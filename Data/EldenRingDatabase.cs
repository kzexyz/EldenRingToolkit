using KsEldenRingToolkitManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
namespace KsEldenRingToolkitManager.Data {
    public static class EldenRingDatabase {
        public const string NexusSource = "https://www.nexusmods.com/eldenring/articles/158";
        public const string BaseGameMirror = "https://raw.githubusercontent.com/Neutron2403/Elden-Ring-Items-ID/refs/heads/main/Elden%20Ring%20Item%20ID.txt";
        public static string LoadStatus {
            get;
            private set;
        }
        = "Not loaded";
        public static List<EldenRingItem> GetItems() {
            List<EldenRingItem> result = new List<EldenRingItem>();
            bool loadedFullNexusArticle = false;
            try {
                string html = DownloadText(NexusSource);
                List<EldenRingItem> nexusItems = ParseNexusArticle(html);
                if (nexusItems.Count> 0) {
                    result.AddRange(nexusItems);
                    loadedFullNexusArticle = true;
                    SaveNexusCache(html);
                }
            } catch {
            }
            if (!loadedFullNexusArticle) {
                try {
                    string?cachedHtml = LoadNexusCache();
                    if (!string.IsNullOrWhiteSpace(cachedHtml)) {
                        List<EldenRingItem> cachedItems = ParseNexusArticle(cachedHtml);
                        if (cachedItems.Count> 0) {
                            result.AddRange(cachedItems);
                            loadedFullNexusArticle = true;
                        }
                    }
                } catch {
                }
            }
            try {
                string mirrorText = DownloadText(BaseGameMirror);
                result.AddRange(ParseBaseGameMirror(mirrorText));
            } catch {
            }
            result.AddRange(GetEmbeddedCharacterCustomizationItems());
            result.AddRange(GetEmbeddedDlcWeaponItems());
            result.AddRange(GetEmbeddedSoteArmorItems());
            foreach (EldenRingItem fallback in GetVerifiedFallbackItems()) {
                result.Add(fallback);
            }
            result = result.GroupBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).Select(group => MergeDuplicateEntries(group.ToList())).ToList();
            LoadStatus = loadedFullNexusArticle?$"Nexus Article 158 database loaded ({result.Count} file entries)" : $"Offline database loaded ({result.Count} file entries, including Shadow of the Erdtree armor and weapons)";
            return result.OrderBy(x => GetCategorySortOrder(x.Category)).ThenBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => ParseNumber(x.ModelId)).ThenBy(x => x.Name).ToList();
        }
        private static string DownloadText(string url) {
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EldenRingToolkit/1.0b");
            return client.GetStringAsync(url).GetAwaiter().GetResult();
        }
        private static List<EldenRingItem> ParseBaseGameMirror(string text) {
            List<EldenRingItem> items = new List<EldenRingItem>();
            Regex armorRegex = new Regex(@"(?im)^(HD|BD|AM|LG)_([MFA])_(\d{4})\s+(.+?)\s*$");
            foreach (Match match in armorRegex.Matches(text)) {
                string prefix = match.Groups[1].Value.ToLowerInvariant();
                string gender = match.Groups[2].Value.ToLowerInvariant();
                string modelId = match.Groups[3].Value;
                string name = match.Groups[4].Value.Trim();
                items.Add(CreatePartItem(name, prefix, gender, modelId, false));
            }
            Regex weaponRegex = new Regex(@"(?im)^WP_A_(\d{2,4})\s+(.+?)\s*$");
            foreach (Match match in weaponRegex.Matches(text)) {
                string modelId = match.Groups[1].Value.PadLeft(4, '0');
                string name = match.Groups[2].Value.Trim();
                items.Add(CreateWeapon(name, modelId, false));
            }
            return items;
        }
        private static List<EldenRingItem> ParseNexusArticle(string html) {
            List<EldenRingItem> items = new List<EldenRingItem>();
            string text = WebUtility.HtmlDecode(html);
            text = Regex.Replace(text, @"(?i)<br\s*/?>", "\n");
            text = Regex.Replace(text, @"(?i)</(?:p|div|li|tr|h\d)>", "\n");
            text = Regex.Replace(text, @"<[^>]+>", "");
            int cutIndex = FindFirstPositiveIndex(text, new[] {
                "91 comments", "Tarnished Edition Items.", "tranished dlc items"
            });
            if (cutIndex >= 0) {
                text = text.Substring(0, cutIndex);
            }
            Regex lineRegex = new Regex(@"(?im)^\s*([a-z]{2}_[a-z]_\d{4}(?:_[a-z])?\.partsbnd\.dcx)\s*-\s*(.+?)\s*$");
            foreach (Match match in lineRegex.Matches(text)) {
                string fileName = match.Groups[1].Value.Trim().ToLowerInvariant();
                string name = CleanArticleName(match.Groups[2].Value);
                if (string.IsNullOrWhiteSpace(name)) {
                    continue;
                }
                EldenRingItem? item = CreateGenericPartItem(fileName, name, IsSoteArticleEntry(match.Index, text));
                if (item != null) {
                    items.Add(item);
                }
            }
            return items;
        }
        private static int FindFirstPositiveIndex(string text, IEnumerable<string> markers) {
            int result = -1;
            foreach (string marker in markers) {
                int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (result<0 || index<result)) {
                    result = index;
                }
            }
            return result;
        }
        private static bool IsSoteArticleEntry(int entryIndex, string articleText) {
            int soteArmorIndex = articleText.IndexOf("Shadow of the Erdtree Armor:", StringComparison.OrdinalIgnoreCase);
            int characterCustomizationIndex = articleText.IndexOf("Character Customization:", StringComparison.OrdinalIgnoreCase);
            return soteArmorIndex >= 0 && entryIndex >= soteArmorIndex && (characterCustomizationIndex<0 || entryIndex<characterCustomizationIndex);
        }
        private static EldenRingItem MergeDuplicateEntries(List<EldenRingItem> entries) {
            EldenRingItem first = entries[0];
            List<string> names = entries.Select(x => x.Name).Where(x =>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new EldenRingItem {
                Name = string.Join(" / ", names), Category = first.Category, Type = first.Type, ModelId = first.ModelId, FileName = first.FileName, Source = first.Source, IsDlc = entries.Any(x => x.IsDlc)
            };
        }
        private static string GetNexusCachePath() {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager", "DatabaseCache");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "nexus_article_158.html");
        }
        private static void SaveNexusCache(string html) {
            try {
                File.WriteAllText(GetNexusCachePath(), html);
            } catch {
            }
        }
        private static string?LoadNexusCache() {
            string path = GetNexusCachePath();
            if (!File.Exists(path)) {
                return null;
            }
            return File.ReadAllText(path);
        }
        private static string CleanArticleName(string value) {
            string result = value.Trim();
            int pipe = result.IndexOf('|');
            if (pipe >= 0) {
                result = result.Substring(0, pipe).Trim();
            }
            return result;
        }
        private static EldenRingItem CreateItem(string name, string prefix, string gender, string modelId, bool isDlc) {
            return CreatePartItem(name, prefix, gender, modelId, isDlc);
        }
        private static EldenRingItem CreatePartItem(string name, string prefix, string gender, string modelId, bool isDlc) {
            string fileName = $"{prefix}_{gender}_{modelId}.partsbnd.dcx";
            return new EldenRingItem {
                Name = name, Category = IsArmorPrefix(prefix)? "Armor" : "Character Customization", Type = DetectPartType(fileName), ModelId = modelId, FileName = fileName, Source = NexusSource, IsDlc = isDlc
            };
        }
        private static EldenRingItem? CreateGenericPartItem(string fileName, string name, bool isDlc) {
            string lower = fileName.ToLowerInvariant();
            Match idMatch = Regex.Match(lower, @"_(\d{4})(?:_[a-z])?\.partsbnd\.dcx$");
            if (!idMatch.Success) {
                return null;
            }
            string modelId = idMatch.Groups[1].Value;
            string type = DetectPartType(lower);
            if (lower.StartsWith("wp_")) {
                type = GetWeaponSubtype(name, modelId);
            }
            if (type == "Unknown") {
                return null;
            }
            string category = lower.StartsWith("wp_")? "Weapon" : (IsArmorType(type)? "Armor" : "Character Customization");
            return new EldenRingItem {
                Name = name, Category = category, Type = type, ModelId = modelId, FileName = lower, Source = NexusSource, IsDlc = isDlc
            };
        }
        private static bool GuessIsDlc(string fileName, int articleIndex, string articleText) {
            string lower = fileName.ToLowerInvariant();
            if (lower.StartsWith("wp_")) {
                int dlcWeapons = articleText.IndexOf("Shadow of the Erdtree Weapons", StringComparison.OrdinalIgnoreCase);
                return dlcWeapons >= 0 && articleIndex >= dlcWeapons;
            }
            if (lower.StartsWith("hd_") || lower.StartsWith("bd_") || lower.StartsWith("am_") || lower.StartsWith("lg_")) {
                int dlcArmor = articleText.IndexOf("Shadow of the Erdtree Armor", StringComparison.OrdinalIgnoreCase);
                int dlcWeapons = articleText.IndexOf("Shadow of the Erdtree Weapons", StringComparison.OrdinalIgnoreCase);
                return dlcArmor >= 0 && articleIndex >= dlcArmor && (dlcWeapons<0 || articleIndex<dlcWeapons);
            }
            return false;
        }
        private static bool IsArmorPrefix(string prefix) {
            string p = prefix.ToLowerInvariant();
            return p == "hd" || p == "bd" || p == "am" || p == "lg";
        }
        private static bool IsArmorType(string type) {
            return type == "Helmet" || type == "Chest Armor" || type == "Gauntlet" || type == "Greaves";
        }
        private static string GetWeaponSubtype(string name, string modelId) {
            string n = name.Trim();
            string lower = n.ToLowerInvariant();
            if (lower.Contains("greatbolt")) {
                return "Greatbolt";
            }
            if (lower.Contains("bolt") &&!lower.Contains("gransax")) {
                return "Bolt";
            }
            if (lower.Contains("great arrow") || lower.Contains("greatarrow")) {
                return "Great Arrow";
            }
            if (lower.EndsWith("arrow") || lower.Contains(" arrow ") || lower.Contains("arrow (")) {
                return "Arrow";
            }
            if (lower.Contains("crossbow")) {
                return "Crossbow";
            }
            if (lower.Contains("ballista") || lower.Contains("jar cannon") || lower.Contains("rabbath's cannon")) {
                return "Ballista";
            }
            if (lower.Contains("greatbow")) {
                return "Greatbow";
            }
            if (lower.Contains("shortbow") || lower.Contains("composite bow") || lower.Contains("harp bow")) {
                return "Shortbow";
            }
            if (lower.Contains("bow")) {
                return "Bow";
            }
            if (lower.Contains("seal")) {
                return "Sacred Seal";
            }
            if (lower.Contains("staff") || lower == "carian regal scepter") {
                return "Glintstone Staff";
            }
            if (lower.Contains("thrusting shield") || lower == "dueling shield" || lower == "carian thrusting shield") {
                return "Thrusting Shield";
            }
            if (lower.Contains("greatshield") || lower.Contains("towershield")) {
                return "Greatshield";
            }
            if (lower.Contains("shield") || lower == "buckler" || lower == "great turtle shell") {
                return "Shield";
            }
            if (lower.Contains("perfume bottle")) {
                return "Perfume Bottle";
            }
            if (lower.Contains("great katana")) {
                return "Great Katana";
            }
            if (lower == "milady") {
                return "Light Greatsword";
            }
            if (lower.Contains("backhand blade") || lower.Contains("cirque")) {
                return "Backhand Blade";
            }
            if (lower.Contains("beast claw") || lower == "red bear's claw") {
                return "Beast Claw";
            }
            if (lower.Contains("smithscript dagger")) {
                return "Dagger";
            }
            if (lower.Contains("smithscript spear")) {
                return "Spear";
            }
            if (lower.Contains("smithscript axe")) {
                return "Axe";
            }
            HashSet<string> katanas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Uchigatana", "Nagakiba", "Hand of Malenia", "Meteoric Ore Blade", "Rivers of Blood", "Moonveil", "Dragonscale Blade", "Serpentbone Blade", "Star-Lined Sword", "Sword of Night"
            };
            if (katanas.Contains(n)) {
                return "Katana";
            }
            HashSet<string> colossalSwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Greatsword", "Watchdog's Greatsword", "Maliketh's Black Blade", "Troll's Golden Sword", "Zweihander", "Starscourge Greatsword", "Royal Greatsword", "Godslayer's Greatsword", "Ruins Greatsword", "Grafted Blade Greatsword", "Troll Knight's Sword"
            };
            if (colossalSwords.Contains(n)) {
                return "Colossal Sword";
            }
            HashSet<string> curvedGreatswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Onyx Lord's Greatsword", "Dismounter", "Bloodhound's Fang", "Magma Wyrm's Scalesword", "Zamor Curved Sword", "Omen Cleaver", "Monk's Flameblade", "Beastman's Cleaver", "Morgott's Cursed Sword", "Horned Warrior's Greatsword", "Freyja's Greatsword", "Putrescence Cleaver"
            };
            if (curvedGreatswords.Contains(n)) {
                return "Curved Greatsword";
            }
            HashSet<string> heavyThrustingSwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Bloody Helice", "Godskin Stitcher", "Great Épée", "Great epee", "Dragon King's Cragblade", "Queelign's Greatsword", "Sword Lance"
            };
            if (heavyThrustingSwords.Contains(n)) {
                return "Heavy Thrusting Sword";
            }
            HashSet<string> thrustingSwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Estoc", "Cleanrot Knight's Sword", "Rapier", "Rogier's Rapier", "Antspur Rapier", "Frozen Needle", "Noble's Estoc", "Carian Sorcery Sword"
            };
            if (thrustingSwords.Contains(n)) {
                return "Thrusting Sword";
            }
            HashSet<string> twinblades = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Twinblade", "Godskin Peeler", "Twinned Knight Swords", "Eleonora's Poleblade", "Gargoyle's Twinblade", "Gargoyle's Black Blades", "Black Steel Twinblade"
            };
            if (twinblades.Contains(n)) {
                return "Twinblade";
            }
            if (lower.Contains("dagger") || lower.Contains("knife") || lower == "misericorde" || lower == "reduvia" || lower == "wakizashi" || lower == "cinquedea" || lower == "ivory sickle" || lower == "blade of calling" || lower == "main-gauche" || lower == "fire knight's shortsword" || lower == "thiollier's hidden needle") {
                return "Dagger";
            }
            if (lower.Contains("scythe")) {
                return "Reaper";
            }
            if (lower.Contains("whip") || lower == "urumi") {
                return "Whip";
            }
            if (lower.Contains("halberd") || lower.Contains("glaive") || lower.Contains("swordspear") || lower == "commander's standard" || lower == "vulgar militia saw" || lower == "vulgar militia shotel") {
                return "Halberd";
            }
            if (lower.Contains("great spear") || lower == "lance" || lower.Contains("treespear") || lower.Contains("serpent-hunter") || lower.Contains("siluria's tree") || lower.Contains("mohgwyn's sacred spear") || lower.Contains("vyke's war spear")) {
                return "Great Spear";
            }
            if (lower.Contains("spear") || lower.Contains("harpoon") || lower == "pike" || lower == "cross-naginata" || lower == "bolt of gransax" || lower == "inquisitor's girandole" || lower == "barbed staff-spear") {
                return "Spear";
            }
            if (lower.Contains("greataxe") || lower.Contains("great axe") || lower == "rusted anchor" || lower == "butchering knife" || lower == "winged greathorn") {
                return "Greataxe";
            }
            if (lower.Contains("axe") || lower.Contains("hatchet") || lower.Contains("cleaver")) {
                return "Axe";
            }
            if (lower.Contains("great hammer") || lower.Contains("greathammer") || lower == "large club" || lower == "great mace" || lower == "great stars" || lower == "brick hammer" || lower == "pickaxe" || lower == "devourer's scepter" || lower == "anvil hammer" || lower == "black steel greathammer" || lower == "smithscript greathammer" || lower == "flowerstone gavel") {
                return "Great Hammer";
            }
            if (lower.Contains("flail") || lower == "family heads" || lower == "bastard's stars") {
                return "Flail";
            }
            if (lower.Contains("hammer") || lower.Contains("mace") || lower.Contains("club") || lower == "warpick" || lower == "morning star" || lower == "ringed finger" || lower == "scepter of the all-knowing") {
                return "Hammer";
            }
            if (lower.Contains("claw") || lower.Contains("talons")) {
                return "Claw";
            }
            if (lower.Contains("fist") || lower.Contains("caestus") || lower == "katar" || lower == "clinging bone" || lower == "veteran's prosthesis" || lower == "cipher pata" || lower == "pata" || lower == "poisoned hand" || lower == "madding hand" || lower == "grafted dragon" || lower == "iron ball") {
                return "Fist";
            }
            if (lower.Contains("torch") || lower == "lamenting visage") {
                return "Torch";
            }
            if (lower.Contains("colossal") || lower == "giant-crusher" || lower == "fallingstar beast jaw" || lower == "ghiza's wheel" || lower == "prelate's inferno crozier" || lower == "dragon greatclaw" || lower == "staff of the avatar" || lower == "golem's halberd" || lower == "troll's hammer" || lower == "rotten staff" || lower == "bloodfiend's arm" || lower == "shadow sunflower blossom" || lower == "gazing finger") {
                return "Colossal Weapon";
            }
            if (lower.Contains("curved sword") || lower == "falchion" || lower == "scimitar" || lower == "shamshir" || lower == "shotel" || lower == "magma blade" || lower == "wing of astel" || lower == "mantis blade" || lower == "grossmesser" || lower == "dancing blade of ranah" || lower == "falx" || lower == "horned warrior's sword" || lower == "spirit sword") {
                return "Curved Sword";
            }
            if (lower.Contains("greatsword") || lower == "claymore" || lower == "flamberge" || lower == "death's poker" || lower == "helphen's steeple" || lower == "sword of milos" || lower == "gargoyle's blackblade" || lower == "ledA's sword".ToLowerInvariant() || lower == "greatsword of solitude" || lower == "fire knight's greatsword" || lower == "ancient meteoric ore greatsword" || lower == "lizard greatsword" || lower == "greatsword of damnation") {
                return "Greatsword";
            }
            if (lower.Contains("sword") || lower == "regalia of eochaid" || lower == "coded sword") {
                return "Straight Sword";
            }
            if (int.TryParse(modelId, out int id) && id >= 100 && id <= 127) {
                return "Dagger";
            }
            return "Weapon";
        }
        private static string DetectPartType(string fileName) {
            string lower = Path.GetFileName(fileName).ToLowerInvariant();
            if (lower.StartsWith("hd_")) {
                return "Helmet";
            }
            if (lower.StartsWith("bd_")) {
                return "Chest Armor";
            }
            if (lower.StartsWith("am_")) {
                return "Gauntlet";
            }
            if (lower.StartsWith("lg_")) {
                return "Greaves";
            }
            if (lower.StartsWith("wp_")) {
                return "Weapon";
            }
            if (lower.StartsWith("hr_")) {
                return "Hairstyle";
            }
            if (lower.StartsWith("fc_")) {
                return "Body";
            }
            if (lower.StartsWith("fg_")) {
                Match idMatch = Regex.Match(lower, @"fg_[amf]_(\d{4})");
                if (!idMatch.Success) {
                    return "Face Part";
                }
                int id = int.Parse(idMatch.Groups[1].Value);
                if (id<1000) {
                    return "Face";
                }
                if (id >= 1500 && id<2000) {
                    return "Eyes";
                }
                if (id >= 2000 && id<3000) {
                    return "Eyebrows";
                }
                if (id >= 3000 && id<4000) {
                    return "Facial Hair";
                }
                if (id >= 5000 && id<6000) {
                    return "Accessory";
                }
                if (id >= 6000 && id<7000) {
                    return "Tattoo / Mark";
                }
                if (id >= 7000 && id<8000) {
                    return "Eyelashes";
                }
                return "Face Part";
            }
            return "Unknown";
        }
        public static string DetectArmorType(string fileName) {
            return DetectPartType(fileName);
        }
        private static EldenRingItem CreateWeapon(string name, string modelId, bool isDlc) {
            return new EldenRingItem {
                Name = name, Category = "Weapon", Type = GetWeaponSubtype(name, modelId), ModelId = modelId, FileName = $"wp_a_{modelId}.partsbnd.dcx", Source = NexusSource, IsDlc = isDlc
            };
        }
        private static string PrefixToType(string prefix) {
            return prefix.ToLowerInvariant() switch {
                "hd" => "Helmet", "bd" => "Chest Armor", "am" => "Gauntlet", "lg" => "Greaves", _ => "Unknown"
            };
        }
        private static int GetCategorySortOrder(string category) {
            return category switch {
                "Armor" => 1, "Weapon" => 2, "Character Customization" => 3, _ => 99
            };
        }
        private static int GetTypeSortOrder(string type) {
            return type switch {
                "Helmet" => 1, "Chest Armor" => 2, "Gauntlet" => 3, "Greaves" => 4, "Dagger" => 10, "Straight Sword" => 11, "Greatsword" => 12, "Colossal Sword" => 13, "Thrusting Sword" => 14, "Heavy Thrusting Sword" => 15, "Curved Sword" => 16, "Curved Greatsword" => 17, "Katana" => 18, "Great Katana" => 19, "Light Greatsword" => 20, "Twinblade" => 21, "Backhand Blade" => 22, "Axe" => 23, "Greataxe" => 24, "Hammer" => 25, "Great Hammer" => 26, "Flail" => 27, "Spear" => 28, "Great Spear" => 29, "Halberd" => 30, "Reaper" => 31, "Whip" => 32, "Fist" => 33, "Claw" => 34, "Beast Claw" => 35, "Colossal Weapon" => 36, "Torch" => 37, "Thrusting Shield" => 38, "Shield" => 39, "Greatshield" => 40, "Glintstone Staff" => 41, "Sacred Seal" => 42, "Shortbow" => 43, "Bow" => 44, "Greatbow" => 45, "Crossbow" => 46, "Ballista" => 47, "Perfume Bottle" => 48, "Arrow" => 49, "Great Arrow" => 50, "Bolt" => 51, "Greatbolt" => 52, "Weapon" => 59, "Hairstyle" => 70, "Face" => 71, "Eyes" => 72, "Eyebrows" => 73, "Facial Hair" => 74, "Accessory" => 75, "Tattoo / Mark" => 76, "Eyelashes" => 77, "Body" => 78, "Face Part" => 79, _ => 99
            };
        }
        private static int ParseNumber(string value) {
            return int.TryParse(value, out int number)? number : int.MaxValue;
        }
        private static List<EldenRingItem> GetEmbeddedCharacterCustomizationItems() {
            List<EldenRingItem> items = new List<EldenRingItem>();
            void Add(string fileName, string name) {
                EldenRingItem? item = CreateGenericPartItem(fileName, name, false);
                if (item != null) {
                    items.Add(item);
                }
            }
            Add("hd_m_0000.partsbnd.dcx", "Blank Head");
            Add("hd_f_0000.partsbnd.dcx", "Blank Head");
            Add("bd_m_0000.partsbnd.dcx", "Blank Armor");
            Add("bd_f_0000.partsbnd.dcx", "Blank Armor");
            Add("am_m_0000.partsbnd.dcx", "Blank Arms");
            Add("am_f_0000.partsbnd.dcx", "Blank Arms");
            Add("lg_m_0000.partsbnd.dcx", "Underwear");
            Add("lg_f_0000.partsbnd.dcx", "Underwear");
            Add("fc_f_0000.partsbnd.dcx", "Young Standard Body");
            Add("fc_f_0001.partsbnd.dcx", "Mature Standard Body");
            Add("fc_f_0002.partsbnd.dcx", "Aged Standard Body");
            Add("fc_f_0100.partsbnd.dcx", "Young Muscular Body");
            Add("fc_f_0101.partsbnd.dcx", "Mature Muscular Body");
            Add("fc_f_0102.partsbnd.dcx", "Aged Muscular Body");
            Add("fc_m_0000.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0000_m.partsbnd.dcx", "Body (Male Variant)");
            Add("fc_m_0001.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0002.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0090.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0100.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0101.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0102.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0105.partsbnd.dcx", "Body (Male)");
            Add("fc_m_0202.partsbnd.dcx", "Body (Male) - Missing Leg");
            Add("fg_a_0000_m.partsbnd.dcx", "Face");
            Add("fg_a_0100.partsbnd.dcx", "Body");
            Add("fg_a_0101.partsbnd.dcx", "Face");
            Add("fg_a_0102.partsbnd.dcx", "Face");
            Add("fg_a_0110.partsbnd.dcx", "Face");
            Add("fg_a_0111.partsbnd.dcx", "Face");
            Add("fg_a_0112.partsbnd.dcx", "Face");
            Add("fg_a_0120.partsbnd.dcx", "Face");
            Add("fg_a_0121.partsbnd.dcx", "Face");
            Add("fg_a_0122.partsbnd.dcx", "Face");
            Add("fg_a_0130.partsbnd.dcx", "Face");
            Add("fg_a_0131.partsbnd.dcx", "Face");
            Add("fg_a_0132.partsbnd.dcx", "Face");
            Add("fg_a_0140.partsbnd.dcx", "Face");
            Add("fg_a_0141.partsbnd.dcx", "Face");
            Add("fg_a_0142.partsbnd.dcx", "Face");
            Add("fg_a_0150.partsbnd.dcx", "Face");
            Add("fg_a_0151.partsbnd.dcx", "Face");
            Add("fg_a_0152.partsbnd.dcx", "Face");
            Add("fg_a_1500.partsbnd.dcx", "Eyeballs - Normal");
            Add("fg_a_1510.partsbnd.dcx", "Eyeballs - All Sclera");
            Add("fg_a_1511.partsbnd.dcx", "Eyeballs - All Pupil");
            Add("fg_a_1512.partsbnd.dcx", "Eyeballs - Normal");
            for (int i = 0; i <= 16; i++) {
                Add($"fg_a_{2000 + i:D4}.partsbnd.dcx", $"Eyebrows Type {i + 1}");
            }
            for (int i = 0; i <= 11; i++) {
                Add($"fg_a_{3000 + i:D4}.partsbnd.dcx", $"Facial Hair Type {i + 1}");
            }
            Add("fg_a_5000.partsbnd.dcx", "Accessory - None");
            Add("fg_a_5001.partsbnd.dcx", "Accessory - Eyepatch (Right)");
            Add("fg_a_5002.partsbnd.dcx", "Accessory - Eyepatch (Left)");
            Add("fg_a_5010.partsbnd.dcx", "Accessory - Blindfold");
            for (int i = 0; i <= 18; i++) {
                Add($"fg_a_{6000 + i:D4}.partsbnd.dcx", $"Tattoo Type {i + 1}");
            }
            for (int i = 20; i <= 38; i++) {
                Add($"fg_a_{6000 + i:D4}.partsbnd.dcx", $"Tattoo Type {i}");
            }
            Add("fg_a_7000.partsbnd.dcx", "Eyelashes - None");
            Add("fg_a_7001.partsbnd.dcx", "Eyelashes - Short");
            Add("fg_a_7002.partsbnd.dcx", "Eyelashes - Medium");
            Add("fg_a_7003.partsbnd.dcx", "Eyelashes - Long");
            Dictionary<string, int> hair = new Dictionary<string, int> {
                ["0000"] = 1, ["0001"] = 4, ["0002"] = 16, ["0003"] = 5, ["0004"] = 17, ["0005"] = 7, ["0006"] = 12, ["0007"] = 13, ["0008"] = 11, ["0009"] = 10, ["0010"] = 8, ["0100"] = 6, ["0101"] = 9, ["0102"] = 18, ["0103"] = 19, ["0104"] = 20, ["0105"] = 21, ["0106"] = 22, ["0107"] = 23, ["0108"] = 25, ["0109"] = 24, ["0110"] = 27, ["0111"] = 26, ["0112"] = 3, ["0113"] = 2, ["0114"] = 15, ["0115"] = 14, ["0116"] = 31, ["0117"] = 28, ["0118"] = 30, ["0119"] = 29, ["0120"] = 35, ["0121"] = 32, ["0122"] = 34, ["0123"] = 36, ["0124"] = 37, ["0125"] = 33
            };
            foreach (KeyValuePair<string, int> pair in hair) {
                Add($"hr_a_{pair.Key}.partsbnd.dcx", $"Hairstyle Type {pair.Value}");
            }
            return items;
        }
        private static List<EldenRingItem> GetEmbeddedDlcWeaponItems() {
            List<EldenRingItem> items = new List<EldenRingItem>();
            void Add(string id, string name) {
                items.Add(CreateWeapon(name, id, true));
            }
            Add("0125", "Main-gauche");
            Add("0127", "Fire Knight's Shortsword");
            Add("0255", "Leda's Sword");
            Add("0270", "Carian Sorcery Sword");
            Add("0271", "Star-Lined Sword");
            Add("0272", "Stone-Sheathed Sword");
            Add("0273", "Sword of Light");
            Add("0274", "Sword of Darkness");
            Add("0275", "Velvet Sword of St. Trina");
            Add("0306", "Queelign's Greatsword");
            Add("0426", "Spirit Sword");
            Add("0427", "Dancing Blade of Ranah");
            Add("0428", "Falx");
            Add("0429", "Horned Warrior's Sword");
            Add("0461", "Putrescence Cleaver");
            Add("0462", "Horned Warrior's Greatsword");
            Add("0463", "Freyja's Greatsword");
            Add("0464", "Greatsword of Radahn");
            Add("0559", "Sword of Night");
            Add("0630", "Moonrithyll's Knight Sword");
            Add("0633", "Unused Sword");
            Add("0634", "Sword Lance");
            Add("0635", "Ancient Meteoric Ore Greatsword");
            Add("0636", "Lizard Greatsword");
            Add("0637", "Greatsword of Damnation");
            Add("0638", "Rellana's Twin Blades");
            Add("0639", "Greatsword of Solitude");
            Add("0640", "Fire Knight's Greatsword");
            Add("0722", "Messmer Soldier's Axe");
            Add("0723", "Death Knight's Twin Axes");
            Add("0724", "Forked-Tongue Hatchet");
            Add("0761", "Bonny Butchering Knife");
            Add("0877", "Devonia's Hammer");
            Add("0878", "Anvil Hammer");
            Add("0879", "Black Steel Greathammer");
            Add("0880", "Smithscript Greathammer");
            Add("0881", "Bloodfiend's Arm");
            Add("0882", "Shadow Sunflower Blossom");
            Add("0883", "Gazing Finger");
            Add("0884", "Flowerstone Gavel");
            Add("0923", "Short Spear");
            Add("0940", "Spear of the Impaler");
            Add("0941", "Messmer Soldier's Spear");
            Add("0942", "Swift Spear");
            Add("0943", "Bloodfiend's Fork");
            Add("0944", "Bloodfiend's Sacred Spear");
            Add("0945", "Barbed Staff-Spear");
            Add("1023", "Spirit Glaive");
            Add("1024", "Death Knight's Longhaft Axe");
            Add("1025", "Poleblade of the Bud");
            Add("1059", "Obsidian Lamina");
            Add("1111", "Euporia");
            Add("1112", "Black Steel Twinblade");
            Add("1167", "Thiollier's Hidden Needle");
            Add("1168", "Poisoned Hand");
            Add("1169", "Madding Hand");
            Add("1170", "Claws of Night");
            Add("1171", "Golem Fist");
            Add("1172", "Shield of Night");
            Add("1209", "Tooth Whip");
            Add("1257", "Serpent Flail");
            Add("1337", "Dryleaf Seal");
            Add("1338", "Staff of the Great Beyond");
            Add("1343", "Fire Knight's Seal");
            Add("1344", "Maternal Staff");
            Add("1345", "Spiraltree Seal");
            Add("1370", "Dryleaf Arts / Dane's Footwork");
            Add("1421", "Bone Bow");
            Add("1422", "Igon's Greatbow");
            Add("1423", "Ansbach's Longbow");
            Add("1515", "Repeating Crossbow");
            Add("1516", "Spread Crossbow");
            Add("1517", "Rabbath's Cannon");
            Add("1530", "Firespark Perfume Bottle");
            Add("1531", "Chilling Perfume Bottle");
            Add("1532", "Frenzyflame Perfume Bottle");
            Add("1533", "Lightning Perfume Bottle");
            Add("1534", "Deadly Poison Perfume Bottle");
            Add("1550", "Smithscript Dagger");
            Add("1551", "Smithscript Spear");
            Add("1552", "Smithscript Axe");
            Add("1570", "Curseblade's Cirque");
            Add("1571", "Backhand Blade");
            Add("1572", "Smithscript Cirque");
            Add("1600", "Dueling Shield");
            Add("1601", "Carian Thrusting Shield");
            Add("1630", "Red Bear's Claw");
            Add("1631", "Beast Claw");
            Add("1650", "Pata");
            Add("1670", "Dragon-Hunter's Great Katana");
            Add("1671", "Great Katana");
            Add("1672", "Rakshasa's Great Katana");
            Add("1680", "Milady");
            Add("2046", "Messmer Soldier Shield");
            Add("2047", "Wolf Crest Shield");
            Add("2048", "Serpent Crest Shield");
            Add("2049", "Golden Lion Shield");
            Add("2120", "Smithscript Shield");
            Add("2229", "Black Steel Greatshield");
            Add("2230", "Verdigris Greatshield");
            Add("3009", "Nanaya's Torch");
            Add("3011", "Lamenting Visage");
            Add("7000", "Piquebone Arrow");
            Add("7500", "Piquebone Bolt");
            Add("7700", "Rabbath's Greatbolt");
            return items;
        }
        private static List<EldenRingItem> GetEmbeddedSoteArmorItems() {
            List<EldenRingItem> items = new List<EldenRingItem>();
            void Add(string fileName, string name) {
                EldenRingItem? item = CreateGenericPartItem(fileName, name, true);
                if (item != null) {
                    items.Add(item);
                }
            }
            Add("hd_m_2700.partsbnd.dcx", "Dane's Hat");
            Add("bd_m_2700.partsbnd.dcx", "Dryleaf Robe");
            Add("am_m_2700.partsbnd.dcx", "Dryleaf Arm Wraps");
            Add("lg_m_2700.partsbnd.dcx", "Dryleaf Cuissardes");
            Add("bd_m_2701.partsbnd.dcx", "Dryleaf Robe (Altered)");
            Add("hd_m_3000.partsbnd.dcx", "Gaius's Helm");
            Add("bd_m_3000.partsbnd.dcx", "Gaius's Armor");
            Add("am_m_3000.partsbnd.dcx", "Gaius's Gauntlets");
            Add("lg_m_3000.partsbnd.dcx", "Gaius's Greaves");
            Add("hd_m_2730.partsbnd.dcx", "Oathseeker Knight Helm");
            Add("bd_m_2730.partsbnd.dcx", "Leda's Armor");
            Add("bd_f_2730.partsbnd.dcx", "Leda's Armor");
            Add("am_m_2730.partsbnd.dcx", "Oathseeker Knight Gauntlets");
            Add("lg_m_2730.partsbnd.dcx", "Oathseeker Knight Greaves");
            Add("bd_m_2735.partsbnd.dcx", "Oathseeker Knight Armor");
            Add("hd_m_2710.partsbnd.dcx", "Verdigris Helm");
            Add("bd_m_2710.partsbnd.dcx", "Verdigris Armor");
            Add("am_m_2710.partsbnd.dcx", "Verdigris Gauntlets");
            Add("lg_m_2710.partsbnd.dcx", "Verdigris Greaves");
            Add("hd_m_2720.partsbnd.dcx", "Pelt of Ralva");
            Add("bd_m_2720.partsbnd.dcx", "Iron Rivet Armor");
            Add("bd_f_2720.partsbnd.dcx", "Iron Rivet Armor");
            Add("am_m_2720.partsbnd.dcx", "Iron Rivet Gauntlets");
            Add("lg_m_2720.partsbnd.dcx", "Iron Rivet Greaves");
            Add("hd_m_2721.partsbnd.dcx", "Fang Helm");
            Add("hd_m_2750.partsbnd.dcx", "Thiollier's Mask");
            Add("bd_m_2750.partsbnd.dcx", "Thiollier's Garb");
            Add("am_m_2750.partsbnd.dcx", "Thiollier's Gloves");
            Add("lg_m_2750.partsbnd.dcx", "Thiollier's Trousers");
            Add("bd_m_2751.partsbnd.dcx", "Thiollier's Garb (Altered)");
            Add("hd_m_2740.partsbnd.dcx", "Dragon-form (Empty)");
            Add("bd_m_2740.partsbnd.dcx", "Dragon-form");
            Add("am_m_2740.partsbnd.dcx", "Dragon-form (Empty)");
            Add("lg_m_2740.partsbnd.dcx", "Dragon-form (Empty)");
            Add("bd_m_2745.partsbnd.dcx", "Dragon-form with Red Armor");
            Add("hd_m_2770.partsbnd.dcx", "High Priest Hat");
            Add("bd_m_2770.partsbnd.dcx", "High Priest Robe");
            Add("bd_f_2770.partsbnd.dcx", "High Priest Robe");
            Add("am_m_2770.partsbnd.dcx", "High Priest Gloves");
            Add("lg_m_2770.partsbnd.dcx", "High Priest Undergarments");
            Add("bd_m_2775.partsbnd.dcx", "Finger Robe");
            Add("hd_m_2790.partsbnd.dcx", "Caterpillar Mask");
            Add("bd_m_2790.partsbnd.dcx", "Braided Cord Robe");
            Add("bd_f_2790.partsbnd.dcx", "Braided Cord Robe");
            Add("am_m_2790.partsbnd.dcx", "Braided Arm Wraps");
            Add("lg_m_2790.partsbnd.dcx", "Soiled Loincloth");
            Add("hd_m_2760.partsbnd.dcx", "Dancer's Hood");
            Add("bd_m_2760.partsbnd.dcx", "Dancer's Dress");
            Add("bd_f_2760.partsbnd.dcx", "Dancer's Dress");
            Add("am_m_2760.partsbnd.dcx", "Dancer's Bracer");
            Add("lg_m_2760.partsbnd.dcx", "Dancer's Trousers");
            Add("lg_f_2760.partsbnd.dcx", "Dancer's Trousers");
            Add("bd_m_2761.partsbnd.dcx", "Dancer's Dress (Altered)");
            Add("bd_f_2761.partsbnd.dcx", "Dancer's Dress (Altered)");
            Add("hd_m_2780.partsbnd.dcx", "Helm of Night");
            Add("bd_m_2780.partsbnd.dcx", "Armor of Night");
            Add("am_m_2780.partsbnd.dcx", "Gauntlets of Night");
            Add("lg_m_2780.partsbnd.dcx", "Greaves of Night");
            Add("hd_m_2850.partsbnd.dcx", "Igon's Helm");
            Add("bd_m_2850.partsbnd.dcx", "Igon's Armor");
            Add("am_m_2850.partsbnd.dcx", "Igon's Gauntlets");
            Add("lg_m_2850.partsbnd.dcx", "Igon's Loincloth");
            Add("hd_m_2851.partsbnd.dcx", "Igon's Helm (Altered)");
            Add("bd_m_2851.partsbnd.dcx", "Igon's Armor (Altered)");
            Add("bd_m_2852.partsbnd.dcx", "Unknown - Ragged Armor");
            Add("lg_m_2852.partsbnd.dcx", "Unknown - Tunic");
            Add("hd_m_2800.partsbnd.dcx", "Wise Man's Mask");
            Add("bd_m_2800.partsbnd.dcx", "Ansbach's Attire");
            Add("am_m_2800.partsbnd.dcx", "Ansbach's Manchettes");
            Add("lg_m_2800.partsbnd.dcx", "Ansbach's Boots");
            Add("bd_m_2801.partsbnd.dcx", "Ansbach's Attire (Altered)");
            Add("hd_m_2810.partsbnd.dcx", "Freyja's Helm");
            Add("bd_m_2810.partsbnd.dcx", "Freyja's Armor");
            Add("bd_f_2810.partsbnd.dcx", "Freyja's Armor");
            Add("am_m_2810.partsbnd.dcx", "Freyja's Gauntlets");
            Add("lg_m_2810.partsbnd.dcx", "Freyja's Greaves");
            Add("lg_f_2810.partsbnd.dcx", "Freyja's Greaves");
            Add("bd_m_2811.partsbnd.dcx", "Freyja's Armor (Altered)");
            Add("bd_f_2811.partsbnd.dcx", "Freyja's Armor (Altered)");
            Add("hd_m_2815.partsbnd.dcx", "Freyja's Helm (Hair Forward)");
            Add("hd_m_2820.partsbnd.dcx", "Helm of Solitude");
            Add("bd_m_2820.partsbnd.dcx", "Armor of Solitude");
            Add("am_m_2820.partsbnd.dcx", "Gauntlets of Solitude");
            Add("lg_m_2820.partsbnd.dcx", "Greaves of Solitude");
            Add("bd_m_2821.partsbnd.dcx", "Armor of Solitude (Altered)");
            Add("hd_m_3050.partsbnd.dcx", "Messmer Soldier Helm");
            Add("bd_m_3050.partsbnd.dcx", "Messmer Soldier Armor");
            Add("am_m_3050.partsbnd.dcx", "Messmer Soldier Gauntlets");
            Add("lg_m_3050.partsbnd.dcx", "Messmer Soldier Greaves");
            Add("bd_m_3051.partsbnd.dcx", "Messmer Soldier Armor (Altered)");
            Add("hd_m_3060.partsbnd.dcx", "Black Knight Helm");
            Add("bd_m_3060.partsbnd.dcx", "Black Knight Armor");
            Add("am_m_3060.partsbnd.dcx", "Black Knight Gauntlets");
            Add("lg_m_3060.partsbnd.dcx", "Black Knight Greaves");
            Add("hd_m_2830.partsbnd.dcx", "Rakshasa Helm");
            Add("bd_m_2830.partsbnd.dcx", "Rakshasa Armor");
            Add("am_m_2830.partsbnd.dcx", "Rakshasa Gauntlets");
            Add("lg_m_2830.partsbnd.dcx", "Rakshasa Greaves");
            Add("hd_m_2840.partsbnd.dcx", "Lamenter-form (Empty)");
            Add("bd_m_2840.partsbnd.dcx", "Lamenter-form");
            Add("am_m_2840.partsbnd.dcx", "Lamenter-form (Empty)");
            Add("lg_m_2840.partsbnd.dcx", "Lamenter-form (Empty)");
            Add("hd_m_3100.partsbnd.dcx", "Fire Knight Helm");
            Add("bd_m_3100.partsbnd.dcx", "Fire Knight Armor");
            Add("am_m_3100.partsbnd.dcx", "Fire Knight Gauntlets");
            Add("lg_m_3100.partsbnd.dcx", "Fire Knight Greaves");
            Add("bd_m_3101.partsbnd.dcx", "Fire Knight Armor (Altered)");
            Add("hd_m_3105.partsbnd.dcx", "Death Mask Helm");
            Add("hd_m_3106.partsbnd.dcx", "Winged Serpent Helm");
            Add("hd_m_3107.partsbnd.dcx", "Salza's Hood");
            Add("hd_m_2860.partsbnd.dcx", "Leather Headband");
            Add("bd_m_2860.partsbnd.dcx", "Gloried Attire");
            Add("am_m_2860.partsbnd.dcx", "Leather Arm Wraps");
            Add("lg_m_2860.partsbnd.dcx", "Leather Leg Wraps");
            Add("hd_m_2861.partsbnd.dcx", "Leather Crown");
            Add("bd_m_2861.partsbnd.dcx", "Highland Attire");
            Add("hd_m_3010.partsbnd.dcx", "Death Knight Helm");
            Add("bd_m_3010.partsbnd.dcx", "Death Knight Armor");
            Add("am_m_3010.partsbnd.dcx", "Death Knight Gauntlets");
            Add("lg_m_3010.partsbnd.dcx", "Death Knight Greaves");
            Add("hd_m_3020.partsbnd.dcx", "Curseblade Mask");
            Add("bd_m_3020.partsbnd.dcx", "Ascetic's Loincloth");
            Add("bd_f_3020.partsbnd.dcx", "Ascetic's Loincloth");
            Add("am_m_3020.partsbnd.dcx", "Ascetic's Wrist Guards");
            Add("lg_m_3020.partsbnd.dcx", "Ascetic's Ankle Guards");
            Add("hd_m_3030.partsbnd.dcx", "Messmer's Helm");
            Add("bd_m_3030.partsbnd.dcx", "Messmer's Armor");
            Add("bd_f_3030.partsbnd.dcx", "Messmer's Armor");
            Add("am_m_3030.partsbnd.dcx", "Messmer's Gauntlets");
            Add("lg_m_3030.partsbnd.dcx", "Messmer's Greaves");
            Add("lg_f_3030.partsbnd.dcx", "Messmer's Greaves");
            Add("hd_m_3031.partsbnd.dcx", "Messmer's Helm (Altered)");
            Add("hd_m_3040.partsbnd.dcx", "Gravebird Helm");
            Add("bd_m_3040.partsbnd.dcx", "Gravebird's Blackquill Armor");
            Add("bd_f_3040.partsbnd.dcx", "Gravebird's Blackquill Armor");
            Add("am_m_3040.partsbnd.dcx", "Gravebird Bracelets");
            Add("lg_m_3040.partsbnd.dcx", "Gravebird Anklets");
            Add("lg_f_3040.partsbnd.dcx", "Gravebird Anklets");
            Add("bd_m_3041.partsbnd.dcx", "Gravebird Armor");
            Add("bd_f_3041.partsbnd.dcx", "Gravebird Armor");
            Add("hd_m_3070.partsbnd.dcx", "Common Soldier Helm");
            Add("bd_m_3070.partsbnd.dcx", "Common Soldier Cloth Armor");
            Add("am_m_3070.partsbnd.dcx", "Common Soldier Gauntlets");
            Add("lg_m_3070.partsbnd.dcx", "Common Soldier Greaves");
            Add("hd_m_3080.partsbnd.dcx", "Horned Warrior Helm");
            Add("bd_m_3080.partsbnd.dcx", "Horned Warrior Armor");
            Add("am_m_3080.partsbnd.dcx", "Horned Warrior Gauntlets");
            Add("lg_m_3080.partsbnd.dcx", "Horned Warrior Greaves");
            Add("hd_m_3085.partsbnd.dcx", "Divine Beast Helm");
            Add("bd_m_3085.partsbnd.dcx", "Divine Beast Warrior Armor");
            Add("hd_m_3086.partsbnd.dcx", "Divine Bird Helm");
            Add("bd_m_3086.partsbnd.dcx", "Divine Bird Warrior Armor");
            Add("am_m_3086.partsbnd.dcx", "Divine Bird Warrior Gauntlets");
            Add("lg_m_3086.partsbnd.dcx", "Divine Bird Warrior Greaves");
            Add("hd_m_3090.partsbnd.dcx", "Rellana's Helm");
            Add("bd_m_3090.partsbnd.dcx", "Rellana's Armor");
            Add("am_m_3090.partsbnd.dcx", "Rellana's Gloves");
            Add("lg_m_3090.partsbnd.dcx", "Rellana's Greaves");
            Add("hd_m_3110.partsbnd.dcx", "Young Lion's Helm");
            Add("bd_m_3110.partsbnd.dcx", "Young Lion's Armor");
            Add("bd_m_3111.partsbnd.dcx", "Young Lion's Armor (Altered)");
            Add("am_m_3110.partsbnd.dcx", "Young Lion's Gauntlets");
            Add("lg_m_3110.partsbnd.dcx", "Young Lion's Greaves");
            Add("hd_m_3120.partsbnd.dcx", "Circlet of Light");
            Add("hd_m_3130.partsbnd.dcx", "Shadow Militiaman Helm");
            Add("bd_m_3130.partsbnd.dcx", "Shadow Militiaman Armor");
            Add("am_m_3130.partsbnd.dcx", "Shadow Militiaman Gauntlets");
            Add("lg_m_3130.partsbnd.dcx", "Shadow Militiaman Greaves");
            Add("hd_m_3140.partsbnd.dcx", "Divine Beast Head");
            Add("hd_m_2870.partsbnd.dcx", "St. Trina's Blossom");
            Add("hd_m_3150.partsbnd.dcx", "Crucible Hammer-Helm");
            Add("hd_m_3160.partsbnd.dcx", "Greatjar");
            Add("hd_m_3170.partsbnd.dcx", "Imp Head (Lion)");
            Add("bd_m_2755.partsbnd.dcx", "Unused Armor?");
            return items;
        }
        private static List<EldenRingItem> GetVerifiedFallbackItems() {
            return new List<EldenRingItem> {
                CreateItem("Iron Helmet", "hd", "m", "1010", false), CreateItem("Scale Armor", "bd", "m", "1010", false), CreateItem("Iron Gauntlets", "am", "m", "1010", false), CreateItem("Leather Trousers", "lg", "m", "1010", false), CreateItem("Black Hood", "hd", "m", "1560", false), CreateItem("Leather Armor", "bd", "m", "1560", false), CreateItem("Leather Gloves", "am", "m", "1560", false), CreateItem("Leather Boots", "lg", "m", "1560", false), CreateItem("Black Wolf Mask", "hd", "m", "1600", false), CreateItem("Blaidd's Armor", "bd", "m", "1600", false), CreateItem("Blaidd's Gauntlets", "am", "m", "1600", false), CreateItem("Blaidd's Greaves", "lg", "m", "1600", false), CreateItem("Black Knife Hood", "hd", "m", "1610", false), CreateItem("Black Knife Armor", "bd", "m", "1610", false), CreateItem("Black Knife Gauntlets", "am", "m", "1610", false), CreateItem("Black Knife Greaves", "lg", "m", "1610", false), CreateItem("Maliketh's Helm", "hd", "m", "1620", false), CreateItem("Maliketh's Armor", "bd", "m", "1620", false), CreateItem("Maliketh's Gauntlets", "am", "m", "1620", false), CreateItem("Maliketh's Greaves", "lg", "m", "1620", false), CreateItem("Godrick Knight Helm", "hd", "m", "2390", false), CreateItem("Godrick Knight Armor", "bd", "m", "2390", false), CreateItem("Godrick Knight Gauntlets", "am", "m", "2390", false), CreateItem("Godrick Knight Greaves", "lg", "m", "2390", false), CreateItem("Dane's Hat", "hd", "m", "2700", true), CreateItem("Dryleaf Robe", "bd", "m", "2700", true), CreateItem("Dryleaf Arm Wraps", "am", "m", "2700", true), CreateItem("Dryleaf Cuissardes", "lg", "m", "2700", true), CreateItem("Verdigris Helm", "hd", "m", "2710", true), CreateItem("Verdigris Armor", "bd", "m", "2710", true), CreateItem("Verdigris Gauntlets", "am", "m", "2710", true), CreateItem("Verdigris Greaves", "lg", "m", "2710", true), CreateItem("Rellana's Helm", "hd", "m", "3090", true), CreateItem("Rellana's Armor", "bd", "m", "3090", true), CreateItem("Rellana's Gloves", "am", "m", "3090", true), CreateItem("Rellana's Greaves", "lg", "m", "3090", true), CreateWeapon("Uchigatana", "0550", false), CreateWeapon("Nagakiba", "0551", false), CreateWeapon("Hand of Malenia", "0552", false), CreateWeapon("Meteoric Ore Blade", "0553", false), CreateWeapon("Rivers of Blood", "0554", false), CreateWeapon("Moonveil", "0556", false), CreateWeapon("Serpentbone Blade", "0557", false), CreateWeapon("Dragonscale Blade", "0558", false), CreateWeapon("Sword of Night", "0559", true), CreateWeapon("Greatsword of Solitude", "0639", true), CreateWeapon("Fire Knight's Greatsword", "0640", true)
            };
        }
    }
}
