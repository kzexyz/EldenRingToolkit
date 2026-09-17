using KsEldenRingToolkitManager.Data;
using KsEldenRingToolkitManager.Models;
using KsEldenRingToolkitManager.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace KsEldenRingToolkitManager {
    public partial class MainWindow : Window {
        private const int EldenRingFirstSlotChecksumOffset = 0x300;
        private const int EldenRingSlotStride = 0x280010;
        private const int EldenRingSlotDataSize = 0x280000;
        private const int EldenRingUserData10ChecksumOffset = 0x19003A0;
        private const int EldenRingUserData10DataOffset = 0x19003B0;
        private const int EldenRingUserData10DataSize = 0x60000;
        private const int EldenRingActiveSlotsRelativeOffset = 0x1954;
        private const int EldenRingProfilesRelativeOffset = 0x195E;
        private const int EldenRingProfileSize = 0x24C;
        private const int EldenRingMinimumPcSaveSize = EldenRingUserData10DataOffset + EldenRingUserData10DataSize;
        private Brush inactiveTopTextBrush = new SolidColorBrush(Color.FromRgb(174, 180, 190));
        private Brush activeTopTextBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private Brush activeLineBrush = new SolidColorBrush(Color.FromRgb(183, 141, 54));
        private Brush sidebarTitleBrush = new SolidColorBrush(Color.FromRgb(119, 126, 138));
        private Brush sidebarNormalTextBrush = new SolidColorBrush(Color.FromRgb(184, 190, 200));
        private Brush sidebarActiveTextBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private Brush sidebarActiveBackgroundBrush = new SolidColorBrush(Color.FromRgb(45, 49, 58));
        private Brush separatorBrush = new SolidColorBrush(Color.FromRgb(54, 58, 69));
        private Brush normalTextBrush = new SolidColorBrush(Color.FromRgb(232, 227, 214));
        private Brush mutedTextBrush = new SolidColorBrush(Color.FromRgb(155, 151, 140));
        private readonly Brush successBrush = new SolidColorBrush(Color.FromRgb(103, 195, 135));
        private readonly Brush errorBrush = new SolidColorBrush(Color.FromRgb(225, 106, 106));
        private Brush warningBrush = new SolidColorBrush(Color.FromRgb(214, 174, 92));
        private readonly List<EldenRingItem> itemDatabase;
        private readonly Dictionary<string, EldenRingItem> itemDatabaseByFileName = new Dictionary<string, EldenRingItem>(StringComparer.OrdinalIgnoreCase);
        private string currentSection = "Save";
        private string currentToolName = "Save Import";
        private bool saveSessionLoaded;
        private string loadedSavePath = "";
        private string transferTargetSavePath = "";
        private int selectedCharacterSlotIndex = -1;
        private string editorSafetyBackupPath = "";
        private Button? activeSidebarButton;
        private ComboBox? targetItemComboBox;
        private TextBlock? targetFilterHintText;
        private TextBox? modFolderTextBox;
        private TextBlock? selectedItemNameText;
        private TextBlock? selectedItemTypeText;
        private TextBlock? selectedItemIdText;
        private TextBlock? selectedItemFileText;
        private TextBlock? modFolderInfoText;
        private TextBlock? detectedModTypeText;
        private TextBlock? detectedModFileText;
        private TextBlock? detectedModItemText;
        private TextBlock? compatibilityText;
        private Button? installButton;
        private readonly List<DetectedModFile> detectedModFiles = new List<DetectedModFile>();
        private string gameModFolder = "";
        private readonly List<DetectedModFile> installedModFiles = new List<DetectedModFile>();
        private bool installedModSnapshotValid;
        private StackPanel? conflictActionPanel;
        private Border? replacementTargetCard;
        private Border? compatibilityCard;
        private Border? conflictCard;
        private Border? installCard;
        private Button? compatibilityCheckButton;
        private Button? conflictCheckButton;
        private TextBlock? conflictStatusText;
        private TextBlock? installSummaryText;
        private ConflictRecommendation? currentRecommendation;
        private readonly List<string> itemReplaceSourcePaths = new List<string>();
        private string itemReplaceSourceDisplay = "No mod source selected";
        private string itemReplaceSourceInfo = "Source: None";
        private string itemReplaceSelectedTargetFileName = "";
        private readonly List<EldenRingItem> itemReplaceTargetCandidates = new List<EldenRingItem>();
        private StackPanel? targetPieceOverridePanel;
        private readonly Dictionary<string, EldenRingItem> itemReplacePieceTargets = new Dictionary<string, EldenRingItem>(StringComparer.OrdinalIgnoreCase);
        private string itemReplaceBaseTargetModelId = "";
        private static readonly HttpClient itemIconHttpClient = new HttpClient {
            Timeout = TimeSpan.FromSeconds(6)
        };
        private static readonly System.Threading.SemaphoreSlim itemIconLoadGate = new System.Threading.SemaphoreSlim(6);
        private static readonly Dictionary<string, string> itemIconUrlCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private ItemReplaceUiState itemReplaceUiState = ItemReplaceUiState.NotChecked;
        private string itemReplaceUiMessage = "Not checked";
        private bool isRestoringItemReplaceUi = false;
        private readonly string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager");
        private string SettingsFilePath => Path.Combine(settingsFolder, "modfolder.txt");
        private string ToolkitSettingsFilePath => Path.Combine(settingsFolder, "toolkit_settings.txt");
        private string PresetsFolderPath => Path.Combine(settingsFolder, "Presets");
        private int installedModsPageSize = 25;
        private string appearancePreset = "Elden Ring Style";
        private const string discordServerUrl = "https://discord.gg/JJCuvBP9m4";
        private const string donateUrl = "https://ko-fi.com/k4neisbak";
        private string appearanceMode = "Dark";
        private int uiScalePercent = 100;
        private bool excludeCpu0;
        private string gameDataFolder = "";
        private DispatcherTimer? cpuAffinityTimer;
        private DispatcherTimer? toastTimer;
        private readonly Dictionary<int, IntPtr> originalProcessAffinity = new Dictionary<int, IntPtr>();
        private TextBox? databaseSearchBox;
        private ComboBox? databaseTypeFilter;
        private ListBox? databaseResultsList;
        private DispatcherTimer? databaseFilterTimer;
        private TextBlock? databaseCountText;
        private TextBlock? databaseDetailsName;
        private TextBlock? databaseDetailsType;
        private TextBlock? databaseDetailsModel;
        private TextBlock? databaseDetailsFile;
        private TextBlock? databaseDetailsGame;
        private Image? databaseDetailsImage;
        private TextBlock? databaseDetailsImageStatus;
        private static bool IsTechnicalBlankItem(EldenRingItem item) {
            return item.ModelId == "0000";
        }
        private static bool IsCharacterCustomizationDatabaseType(string?type) {
            return type is not null && (type.Equals("Hairstyle", StringComparison.OrdinalIgnoreCase) || type.Equals("Hair", StringComparison.OrdinalIgnoreCase) || type.Equals("Face", StringComparison.OrdinalIgnoreCase) || type.Equals("Eyes", StringComparison.OrdinalIgnoreCase) || type.Equals("Eyebrows", StringComparison.OrdinalIgnoreCase) || type.Equals("Facial Hair", StringComparison.OrdinalIgnoreCase) || type.Equals("Accessory", StringComparison.OrdinalIgnoreCase) || type.Equals("Cosmetic", StringComparison.OrdinalIgnoreCase) || type.Equals("Eyepatch", StringComparison.OrdinalIgnoreCase) || type.Equals("Eye Patch", StringComparison.OrdinalIgnoreCase) || type.Equals("Tattoo / Mark", StringComparison.OrdinalIgnoreCase) || type.Equals("Eyelashes", StringComparison.OrdinalIgnoreCase) || type.Equals("Body", StringComparison.OrdinalIgnoreCase) || type.Equals("Face Part", StringComparison.OrdinalIgnoreCase));
        }
        private static bool IsCharacterCustomizationDatabaseEntry(EldenRingItem item) {
            if (IsCharacterCustomizationDatabaseType(item.Type)) return true;
            string name = item.Name ?? string.Empty;
            return name.StartsWith("Hairstyle Type ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Face Type ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Eyebrow Type ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Facial Hair Type ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Eyepatch Type ", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsKnownTechnicalDatabaseEntry(EldenRingItem item) {
            string name = item.Name ?? string.Empty;
            return name.EndsWith(" (Empty)", StringComparison.OrdinalIgnoreCase) || name.Equals("Dragon-form", StringComparison.OrdinalIgnoreCase) || name.Equals("Dragon-form with Red Armor", StringComparison.OrdinalIgnoreCase) || name.Equals("Lamenter-form", StringComparison.OrdinalIgnoreCase);
        }
        private bool IsBrowsableItemDatabaseEntry(EldenRingItem item) {
            if (IsTechnicalBlankItem(item)) return false;
            if (IsCharacterCustomizationDatabaseEntry(item) || IsKnownTechnicalDatabaseEntry(item)) return false;
            if (!EldenRingGameIconService.HasDeterministicIconMapping(item)) return false;
            if (HasValidGameDataDirectory() && File.Exists(Path.Combine(settingsFolder, "ItemIconsGameV7", ".native-icons-ready-v1")) &&!File.Exists(GetDatabaseGameItemIconCachePath(item))) {
                return false;
            }
            return true;
        }
        private static readonly System.Threading.SemaphoreSlim VisualIconUpgradeGate = new(1, 1);
        private readonly System.Threading.SemaphoreSlim fullIconPrecacheGate = new(1, 1);
        private bool fullIconPrecacheRunning;
        public MainWindow() {
            InitializeComponent();
            itemDatabase = EldenRingDatabase.GetItems().Where(item =>!IsTechnicalBlankItem(item)).ToList();
            foreach (EldenRingItem item in itemDatabase) {
                if (!itemDatabaseByFileName.ContainsKey(item.FileName)) {
                    itemDatabaseByFileName[item.FileName] = item;
                }
            }
            LoadSavedModFolder();
            LoadToolkitSettings();
            ApplyAppearanceSettings();
            ApplyUiScaleSetting();
            UpdateCpuAffinityMonitoring();
            Closed += MainWindow_Closed;
            Loaded += MainWindow_IconPrecacheLoaded;
            UpdateSaveDependentNavigation();
            SelectTopSection("Save", SaveButton);
        }
        private async void MainWindow_IconPrecacheLoaded(object sender, RoutedEventArgs e) {
            if (HasValidGameDataDirectory()) {
                await StartFullGameIconPrecacheAsync(false);
            }
        }
        private async System.Threading.Tasks.Task StartFullGameIconPrecacheAsync(bool announceAlreadyReady) {
            if (!HasValidGameDataDirectory()) return;
            string folderSnapshot = gameDataFolder;
            await fullIconPrecacheGate.WaitAsync();
            try {
                if (!string.Equals(folderSnapshot, gameDataFolder, StringComparison.OrdinalIgnoreCase)) return;
                bool ready = EldenRingGameIconService.IsFullCacheReady(folderSnapshot, itemDatabase, GetDatabaseGameItemIconCachePath);
                if (ready) {
                    EldenRingGameIconService.ReleaseRuntimeCache();
                    if (announceAlreadyReady) ShowToast("High-resolution item icons are already cached", ToastKind.Success);
                    return;
                }
                fullIconPrecacheRunning = true;
                ShowToast("Loading Elden Ring high-resolution item icons • Please be patient", ToastKind.Info);
                StatusText.Text = "Preparing high-resolution item icons from game files...";
                Progress<EldenRingGameIconService.PrecacheProgress> progress = new Progress<EldenRingGameIconService.PrecacheProgress>(p => {
                    if (!string.Equals(folderSnapshot, gameDataFolder, StringComparison.OrdinalIgnoreCase)) return; StatusText.Text = p.Message;
                });
                EldenRingGameIconService.PrecacheResult result = await EldenRingGameIconService.PrecacheAllHighResolutionIconsAsync(folderSnapshot, itemDatabase, GetDatabaseGameItemIconCachePath, progress);
                if (!string.Equals(folderSnapshot, gameDataFolder, StringComparison.OrdinalIgnoreCase)) return;
                if (result.Complete) {
                    string skippedSuffix = result.SkippedWithoutNativeCell> 0?$" • {result.SkippedWithoutNativeCell} technical rows skipped" : string.Empty;
                    StatusText.Text = $"High-resolution item icon cache ready • {result.TotalMapped} icons{skippedSuffix}";
                    ShowToast($"High-resolution item icons ready • {result.TotalMapped} cached", ToastKind.Success);
                    RefreshDatabaseResults();
                    if (databaseResultsList?.SelectedItem is EldenRingItem selectedDatabaseItem) UpdateDatabaseItemImage(selectedDatabaseItem);
                } else {
                    StatusText.Text = "High-resolution icon cache incomplete • icons will retry when needed";
                    ShowToast("Some high-resolution icons could not be cached", ToastKind.Info);
                }
            } catch (Exception ex) {
                StatusText.Text = "High-resolution icon cache failed";
                Debug.WriteLine(ex);
            } finally {
                fullIconPrecacheRunning = false;
                fullIconPrecacheGate.Release();
            }
        }
        private void TopNavigation_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button clickedButton) {
                return;
            }
            string sectionName = clickedButton.Content?.ToString() ?? "";
            if (IsSaveDependentSection(sectionName) &&!saveSessionLoaded) {
                ShowToast("Select a save first • Save > Save Import", ToastKind.Info);
                SelectTopSection("Save", SaveButton);
                OpenToolPage("Save Import");
                return;
            }
            SelectTopSection(sectionName, clickedButton);
        }
        private void SelectTopSection(string sectionName, Button activeButton) {
            currentSection = sectionName;
            UpdateTopHighlight(activeButton);
            BuildSidebar(sectionName);
        }
        private void UpdateTopHighlight(Button activeButton) {
            Button[] buttons = {
                SaveButton, CharacterButton, InventoryButton, PresetsButton, SkinButton, AboutButton, SettingsButton
            };
            foreach (Button button in buttons) {
                button.Foreground = inactiveTopTextBrush;
                button.FontWeight = FontWeights.Normal;
                button.BorderBrush = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
            }
            activeButton.Foreground = activeTopTextBrush;
            activeButton.FontWeight = FontWeights.SemiBold;
            activeButton.BorderBrush = activeLineBrush;
            activeButton.BorderThickness = new Thickness(0, 0, 0, 2);
        }
        private void BuildSidebar(string sectionName) {
            SidebarPanel.Children.Clear();
            activeSidebarButton = null;
            switch (sectionName) {
                case "Save" : AddSidebarSection("SAVE", "Save Import", "Backup & Restore", "Character Manager");
                break;
                case "Character" : AddSidebarSection("CHARACTER", "Character Information", "Stats", "Level", "Runes");
                break;
                case "Inventory" : AddSidebarSection("INVENTORY", "Weapons", "Armor", "Talismans", "Consumables", "Sorceries", "Incantations", "Spirit Ashes", "Ashes of War", "Key Items");
                break;
                case "Presets" : AddSidebarSection("PRESETS", "Import Preset", "Export Preset", "Delete / Clear Preset");
                break;
                case "Skin" : AddSidebarSection("SKIN MOD", "Item Replace", "Installed Mods");
                AddSidebarSeparator();
                AddSidebarSection("TOOLS", "Item Database", "Mod Engine 2 Folder");
                break;
                case "About" : AddSidebarSection("ABOUT", "Overview", "Version", "Credits");
                break;
                case "Settings" : AddSidebarSection("SETTINGS", "General", "Appearance", "Performance");
                break;
            }
            SelectFirstSidebarButton();
        }
        private void AddSidebarSection(string title, params string[] items) {
            TextBlock sectionTitle = new TextBlock {
                Text = title, Foreground = sidebarTitleBrush, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(20, 25, 0, 12)
            };
            SidebarPanel.Children.Add(sectionTitle);
            foreach (string item in items) {
                Button button = new Button {
                    Content = item, Style = (Style) FindResource("SideButton"), Foreground = sidebarNormalTextBrush
                };
                button.Click += SidebarButton_Click;
                SidebarPanel.Children.Add(button);
            }
        }
        private void AddSidebarSeparator() {
            Separator separator = new Separator {
                Margin = new Thickness(20, 15, 20, 0), Background = separatorBrush
            };
            SidebarPanel.Children.Add(separator);
        }
        private void SelectFirstSidebarButton() {
            foreach (UIElement element in SidebarPanel.Children) {
                if (element is Button button) {
                    SelectSidebarButton(button);
                    return;
                }
            }
        }
        private void NavigateToSidebarTool(string toolName) {
            foreach (UIElement element in SidebarPanel.Children) {
                if (element is Button button && string.Equals(button.Content?.ToString(), toolName, StringComparison.Ordinal)) {
                    SelectSidebarButton(button);
                    return;
                }
            }
            OpenToolPage(toolName);
        }
        private void SidebarButton_Click(object sender, RoutedEventArgs e) {
            if (sender is not Button button) {
                return;
            }
            SelectSidebarButton(button);
        }
        private void SelectSidebarButton(Button selectedButton) {
            if (activeSidebarButton != null) {
                activeSidebarButton.Background = Brushes.Transparent;
                activeSidebarButton.Foreground = sidebarNormalTextBrush;
                activeSidebarButton.FontWeight = FontWeights.Normal;
            }
            selectedButton.Background = sidebarActiveBackgroundBrush;
            selectedButton.Foreground = sidebarActiveTextBrush;
            selectedButton.FontWeight = FontWeights.SemiBold;
            activeSidebarButton = selectedButton;
            string toolName = selectedButton.Content?.ToString() ?? "";
            OpenToolPage(toolName);
        }
        private void OpenToolPage(string toolName) {
            currentToolName = toolName;
            StatusText.Text = currentSection + " > " + toolName;
            if (currentSection == "Save" && toolName == "Save Import") {
                BuildSaveImportPage();
                return;
            }
            if (currentSection == "Save" && toolName == "Character Manager") {
                BuildCharacterManagerPage();
                return;
            }
            if (currentSection == "Save" && toolName == "Backup & Restore") {
                BuildSaveBackupRestorePage();
                return;
            }
            if (currentSection == "Character") {
                switch (toolName) {
                    case "Character Information" : BuildCharacterInformationPage();
                    return;
                    case "Stats" : BuildCharacterStatsPage();
                    return;
                    case "Level" : BuildCharacterLevelPage();
                    return;
                    case "Runes" : BuildCharacterRunesPage();
                    return;
                }
            }
            if (currentSection == "Inventory") {
                BuildInventoryPage(toolName);
                return;
            }
            if (currentSection == "Presets") {
                switch (toolName) {
                    case "Import Preset" : BuildImportPresetPage();
                    return;
                    case "Export Preset" : BuildExportPresetPage();
                    return;
                    case "Delete / Clear Preset" : BuildDeleteClearPresetPage();
                    return;
                }
            }
            if (currentSection == "Skin" && toolName == "Item Replace") {
                BuildItemReplacePage();
                return;
            }
            if (currentSection == "Skin" && toolName == "Installed Mods") {
                BuildInstalledModsPage();
                return;
            }
            if (currentSection == "Skin" && toolName == "Item Database") {
                BuildItemDatabasePage();
                return;
            }
            if (currentSection == "Skin" && (toolName == "Mod Engine 2 Folder" || toolName == "Mod Folder")) {
                BuildModFolderPage();
                return;
            }
            if (currentSection == "About") {
                switch (toolName) {
                    case "Overview" : BuildAboutOverviewPage();
                    return;
                    case "Version" : BuildAboutVersionPage();
                    return;
                    case "Credits" : BuildAboutCreditsPage();
                    return;
                }
            }
            if (currentSection == "Settings") {
                switch (toolName) {
                    case "General" : BuildSettingsPathsPage();
                    return;
                    case "Appearance" : BuildSettingsInterfacePage();
                    return;
                    case "Performance" : BuildSettingsPerformancePage();
                    return;
                }
            }
            ShowGenericPage(toolName, currentSection);
        }
        private bool IsSaveDependentSection(string sectionName) {
            return sectionName == "Character" || sectionName == "Inventory" || sectionName == "Presets";
        }
        private void UpdateSaveDependentNavigation() {
            Button[] dependentButtons = {
                CharacterButton, InventoryButton, PresetsButton
            };
            foreach (Button button in dependentButtons) {
                button.Opacity = saveSessionLoaded? 1.0 : 0.45;
                button.ToolTip = saveSessionLoaded? null : "Requires a loaded Elden Ring save file. Go to Save > Save Import.";
            }
        }
        private void BuildSaveImportPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Save Import", "Load an Elden Ring PC save file once, then Character, Inventory, and Presets become available."));
            Border statusCard = CreateCard();
            statusCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel statusStack = new StackPanel();
            statusStack.Children.Add(CreateSectionTitle("SAVE SESSION"));
            statusStack.Children.Add(CreateInfoLine("Status", saveSessionLoaded? "Loaded" : "No save loaded"));
            statusStack.Children.Add(CreateInfoLine("Save File", saveSessionLoaded? loadedSavePath : "None"));
            statusStack.Children.Add(CreateInfoLine("Save Type", saveSessionLoaded?(Path.GetExtension(loadedSavePath).Equals(".co2", StringComparison.OrdinalIgnoreCase)? "Seamless Co-op" : "Original Game") : "None"));
            if (saveSessionLoaded && File.Exists(loadedSavePath)) {
                FileInfo fileInfo = new FileInfo(loadedSavePath);
                statusStack.Children.Add(CreateInfoLine("File Size", FormatFileSize(fileInfo.Length)));
                statusStack.Children.Add(CreateInfoLine("Last Modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
            }
            statusCard.Child = statusStack;
            root.Children.Add(statusCard);
            Border actionsCard = CreateCard();
            StackPanel actionsStack = new StackPanel();
            actionsStack.Children.Add(CreateSectionTitle("SELECT SAVE FILE"));
            actionsStack.Children.Add(CreateFieldLabel("Select ER0000.sl2 manually, or let the toolkit search the default Elden Ring Steam save folders."));
            StackPanel buttons = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0)
            };
            Button browseButton = new Button {
                Content = "Browse Save...", Style = (Style) FindResource("ActionButton")
            };
            browseButton.Click += BrowseSaveFile_Click;
            Button autoFindButton = new Button {
                Content = "Auto Find", Style = (Style) FindResource("PrimaryButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            autoFindButton.Click += AutoFindSave_Click;
            buttons.Children.Add(browseButton);
            buttons.Children.Add(autoFindButton);
            if (saveSessionLoaded) {
                Button unloadButton = new Button {
                    Content = "Unload Save", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
                };
                unloadButton.Click += UnloadSave_Click;
                buttons.Children.Add(unloadButton);
            }
            actionsStack.Children.Add(buttons);
            actionsCard.Child = actionsStack;
            root.Children.Add(actionsCard);
            Border dependencyCard = CreateCard();
            StackPanel dependencyStack = new StackPanel();
            dependencyStack.Children.Add(CreateSectionTitle("SAVE-DEPENDENT TOOLS"));
            dependencyStack.Children.Add(CreateInfoLine("Character", saveSessionLoaded? "Unlocked" : "Locked"));
            dependencyStack.Children.Add(CreateInfoLine("Inventory", saveSessionLoaded? "Unlocked" : "Locked"));
            dependencyStack.Children.Add(CreateInfoLine("Presets", saveSessionLoaded? "Unlocked" : "Locked"));
            dependencyCard.Child = dependencyStack;
            root.Children.Add(dependencyCard);
            ContentHost.Children.Add(root);
            StatusText.Text = saveSessionLoaded? "Save > Save Import > Save loaded" : "Save > Save Import > Waiting for save";
        }
        private void BrowseSaveFile_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog {
                Title = "Select Elden Ring Save File", Filter = "Elden Ring Saves (*.sl2;*.co2)|*.sl2;*.co2|Original Save (*.sl2)|*.sl2|Seamless Co-op Save (*.co2)|*.co2|All Files (*.*)|*.*", Multiselect = false
            };
            bool?result = dialog.ShowDialog();
            if (result != true) {
                return;
            }
            LoadSaveSession(dialog.FileName);
        }
        private void AutoFindSave_Click(object sender, RoutedEventArgs e) {
            List<string> saves = FindDefaultEldenRingSaves();
            if (saves.Count == 0) {
                MessageBox.Show("No Elden Ring save files were found in the default save location." + "\n\n" + "Supported active save files:" + "\n" + "• ER0000.sl2 — original game" + "\n" + "• ER0000.co2 — Seamless Co-op" + "\n\n" + "You can still use Browse Save to select one manually.", "No Save Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string?selectedPath = ShowAutoFindSavePicker(saves);
            if (string.IsNullOrWhiteSpace(selectedPath)) {
                return;
            }
            LoadSaveSession(selectedPath);
        }
        private List<string> FindDefaultEldenRingSaves() {
            List<string> result = new List<string>();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string eldenRingFolder = Path.Combine(appData, "EldenRing");
            if (!Directory.Exists(eldenRingFolder)) {
                return result;
            }
            try {
                result.AddRange(Directory.GetFiles(eldenRingFolder, "ER0000.sl2", SearchOption.AllDirectories));
                result.AddRange(Directory.GetFiles(eldenRingFolder, "ER0000.co2", SearchOption.AllDirectories));
            } catch {
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => Path.GetExtension(path).Equals(".sl2", StringComparison.OrdinalIgnoreCase)? 0 : 1).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }
        private void ApplyFluentPopupResources(FrameworkElement target) {
            string[] brushKeys = {
                "BgMain", "BgPanel", "BgPanel2", "BgHover", "BgPressed", "BorderMain", "BorderSoft", "TextMain", "TextSoft", "TextMuted", "Accent", "AccentBright", "DropBorder", "DropHover", "DropSelected", "DropArrow", "DropItemText", "AccentSelection", "AccentButton", "AccentButtonHover"
            };
            foreach (string key in brushKeys) {
                object?resource = TryFindResource(key);
                if (resource != null) {
                    target.Resources[key] = resource;
                }
            }
            object?thumbStyle = TryFindResource("FluentScrollThumb");
            if (thumbStyle != null) {
                target.Resources["FluentScrollThumb"] = thumbStyle;
            }
            if (TryFindResource(typeof(ScrollBar)) is Style scrollBarStyle) {
                target.Resources[typeof(ScrollBar)] = scrollBarStyle;
            }
        }
        private string?ShowAutoFindSavePicker(List<string> saves) {
            string?selectedPath = null;
            Dictionary<ListBoxItem, Border> rowBorders = new Dictionary<ListBoxItem, Border>();
            Window pickerWindow = new Window {
                Title = "Select Elden Ring Save File", Width = 820, Height = 450, MinWidth = 680, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = GetResourceBrush("BgMain"), Foreground = GetResourceBrush("TextMain"), ResizeMode = ResizeMode.CanResize
            };
            ApplyFluentPopupResources(pickerWindow);
            Grid root = new Grid {
                Margin = new Thickness(16)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            StackPanel header = new StackPanel {
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.Children.Add(new TextBlock {
                Text = "Found " + saves.Count + " active save file" + (saves.Count == 1? "" : "s"), FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("TextMain")
            });
            header.Children.Add(new TextBlock {
                Text = "Select a save file to load.", Margin = new Thickness(0, 4, 0, 0), Foreground = GetResourceBrush("TextSoft")
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);
            ListBox listBox = new ListBox {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(3), HorizontalContentAlignment = HorizontalAlignment.Stretch, SelectionMode = SelectionMode.Single, SnapsToDevicePixels = false
            };
            Border listFrame = new Border {
                Background = GetResourceBrush("BgPanel"), BorderBrush = GetResourceBrush("BorderMain"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(2), SnapsToDevicePixels = false
            };
            listFrame.Child = listBox;
            ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
            Style plainItemStyle = new Style(typeof(ListBoxItem));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(1, 0, 1, 5)));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));
            FrameworkElementFactory itemRoot = new FrameworkElementFactory(typeof(Border));
            itemRoot.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemRoot.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            itemRoot.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            itemRoot.SetValue(Border.SnapsToDevicePixelsProperty, false);
            FrameworkElementFactory itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            itemPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            itemPresenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            itemPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            itemPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            itemRoot.AppendChild(itemPresenter);
            ControlTemplate plainItemTemplate = new ControlTemplate(typeof(ListBoxItem)) {
                VisualTree = itemRoot
            };
            plainItemStyle.Setters.Add(new Setter(ListBoxItem.TemplateProperty, plainItemTemplate));
            listBox.ItemContainerStyle = plainItemStyle;
            foreach (string savePath in saves) {
                string extension = Path.GetExtension(savePath);
                string type = extension.Equals(".co2", StringComparison.OrdinalIgnoreCase)? "Seamless" : "Original";
                string accountFolder = Directory.GetParent(savePath)?.Name ?? "Unknown";
                FileInfo info = new FileInfo(savePath);
                Border rowBorder = new Border {
                    Background = GetResourceBrush("BgPanel2"), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), SnapsToDevicePixels = false
                };
                Grid rowGrid = new Grid {
                    Margin = new Thickness(9, 6, 9, 6)
                };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(88)
                });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(132)
                });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                TextBlock typeText = new TextBlock {
                    Text = type, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("AccentBright")
                };
                Grid.SetColumn(typeText, 0);
                rowGrid.Children.Add(typeText);
                StackPanel accountStack = new StackPanel {
                    VerticalAlignment = VerticalAlignment.Center
                };
                accountStack.Children.Add(new TextBlock {
                    Text = accountFolder, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("TextMain")
                });
                accountStack.Children.Add(new TextBlock {
                    Text = extension.ToUpperInvariant(), Margin = new Thickness(0, 1, 0, 0), FontSize = 11, Foreground = GetResourceBrush("TextMuted")
                });
                Grid.SetColumn(accountStack, 1);
                rowGrid.Children.Add(accountStack);
                StackPanel pathStack = new StackPanel {
                    VerticalAlignment = VerticalAlignment.Center
                };
                pathStack.Children.Add(new TextBlock {
                    Text = savePath, Foreground = GetResourceBrush("TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis
                });
                pathStack.Children.Add(new TextBlock {
                    Text = "Modified " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), Margin = new Thickness(0, 1, 0, 0), FontSize = 11, Foreground = GetResourceBrush("TextMuted")
                });
                Grid.SetColumn(pathStack, 2);
                rowGrid.Children.Add(pathStack);
                rowBorder.Child = rowGrid;
                ListBoxItem item = new ListBoxItem {
                    Content = rowBorder, Tag = savePath, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FocusVisualStyle = null
                };
                item.MouseEnter += (_, _) => {
                    if (!item.IsSelected) {
                        rowBorder.Background = GetResourceBrush("BgHover");
                    }
                };
                item.MouseLeave += (_, _) => {
                    if (!item.IsSelected) {
                        rowBorder.Background = GetResourceBrush("BgPanel2");
                    }
                };
                rowBorders[item] = rowBorder;
                listBox.Items.Add(item);
            }
            Grid.SetRow(listFrame, 1);
            root.Children.Add(listFrame);
            StackPanel footer = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0)
            };
            Button cancelButton = new Button {
                Content = "Cancel", Style = (Style) FindResource("ActionButton"), MinWidth = 96
            };
            Button selectButton = new Button {
                Content = "Select", Style = (Style) FindResource("ActionButton"), MinWidth = 104, Margin = new Thickness(8, 0, 0, 0), Opacity = 0.55, IsHitTestVisible = false
            };
            listBox.SelectionChanged += (_, _) => {
                foreach (KeyValuePair<ListBoxItem, Border> pair in rowBorders) {
                    bool selected = pair.Key.IsSelected;
                    pair.Value.Background = selected? GetResourceBrush("AccentSelection") : GetResourceBrush("BgPanel2");
                    pair.Value.BorderBrush = selected? GetResourceBrush("AccentBright") : Brushes.Transparent;
                    pair.Value.BorderThickness = new Thickness(1);
                }
                bool hasSelection = listBox.SelectedItem is ListBoxItem;
                selectButton.IsHitTestVisible = hasSelection;
                selectButton.Opacity = hasSelection? 1.0 : 0.55;
                selectButton.Background = hasSelection? GetResourceBrush("AccentButton") : GetResourceBrush("BgPanel2");
                selectButton.BorderBrush = hasSelection? GetResourceBrush("Accent") : GetResourceBrush("BorderSoft");
            };
            listBox.MouseDoubleClick += (_, _) => {
                if (listBox.SelectedItem is ListBoxItem item && item.Tag is string path) {
                    selectedPath = path;
                    pickerWindow.DialogResult = true;
                }
            };
            cancelButton.Click += (_, _) => {
                pickerWindow.DialogResult = false;
            };
            selectButton.Click += (_, _) => {
                if (listBox.SelectedItem is not ListBoxItem item || item.Tag is not string path) {
                    return;
                }
                selectedPath = path;
                pickerWindow.DialogResult = true;
            };
            footer.Children.Add(cancelButton);
            footer.Children.Add(selectButton);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            pickerWindow.Content = root;
            pickerWindow.ShowDialog();
            return selectedPath;
        }
        private void LoadSaveSession(string path) {
            if (string.IsNullOrWhiteSpace(path) ||!File.Exists(path)) {
                MessageBox.Show("The selected save file does not exist.", "Invalid Save File", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string extension = Path.GetExtension(path);
            bool supported = extension.Equals(".sl2", StringComparison.OrdinalIgnoreCase) || extension.Equals(".co2", StringComparison.OrdinalIgnoreCase);
            if (!supported) {
                MessageBox.Show("Please select a supported Elden Ring save file." + "\n\n" + "Supported formats:" + "\n" + "• .sl2 — original game" + "\n" + "• .co2 — Seamless Co-op", "Invalid Save File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            loadedSavePath = path;
            editorSafetyBackupPath = "";
            saveSessionLoaded = true;
            UpdateSaveDependentNavigation();
            BuildSaveImportPage();
            ShowToast("Save loaded • Character, Inventory and Presets unlocked", ToastKind.Success);
        }
        private void ShowToast(string message, ToastKind kind) {
            if (ToastHost == null || ToastMessage == null || ToastIcon == null || ToastTransform == null) {
                return;
            }
            toastTimer?.Stop();
            ToastMessage.Text = message;
            switch (kind) {
                case ToastKind.Success : ToastIcon.Text = "✓";
                ToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(79, 209, 139));
                ToastHost.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 132, 91));
                break;
                case ToastKind.Warning : ToastIcon.Text = "!";
                ToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(224, 176, 87));
                ToastHost.BorderBrush = new SolidColorBrush(Color.FromRgb(135, 103, 48));
                break;
                default : ToastIcon.Text = "i";
                ToastIcon.Foreground = GetResourceBrush("AccentBright");
                ToastHost.BorderBrush = GetResourceBrush("Accent");
                break;
            }
            ToastHost.Visibility = Visibility.Visible;
            ToastHost.Opacity = 0;
            ToastTransform.X = 28;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
            DoubleAnimation slideIn = new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(190)) {
                EasingFunction = new CubicEase {
                    EasingMode = EasingMode.EaseOut
                }
            };
            ToastHost.BeginAnimation(OpacityProperty, fadeIn);
            ToastTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            toastTimer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(2.8)
            };
            toastTimer.Tick += (_, _) => {
                toastTimer.Stop();
                HideToast();
            };
            toastTimer.Start();
        }
        private void HideToast() {
            if (ToastHost == null || ToastTransform == null) {
                return;
            }
            DoubleAnimation fadeOut = new DoubleAnimation(ToastHost.Opacity, 0, TimeSpan.FromMilliseconds(170));
            DoubleAnimation slideOut = new DoubleAnimation(ToastTransform.X, 22, TimeSpan.FromMilliseconds(170)) {
                EasingFunction = new CubicEase {
                    EasingMode = EasingMode.EaseIn
                }
            };
            fadeOut.Completed += (_, _) => {
                ToastHost.Visibility = Visibility.Collapsed;
                ToastHost.BeginAnimation(OpacityProperty, null);
                ToastTransform.BeginAnimation(TranslateTransform.XProperty, null);
                ToastHost.Opacity = 0;
                ToastTransform.X = 28;
            };
            ToastHost.BeginAnimation(OpacityProperty, fadeOut);
            ToastTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }
        private void UnloadSave_Click(object sender, RoutedEventArgs e) {
            loadedSavePath = "";
            editorSafetyBackupPath = "";
            selectedCharacterSlotIndex = -1;
            saveSessionLoaded = false;
            UpdateSaveDependentNavigation();
            BuildSaveImportPage();
        }
        private static string FormatFileSize(long bytes) {
            double value = bytes;
            string[] units = {
                "B", "KB", "MB", "GB"
            };
            int unit = 0;
            while (value >= 1024 && unit<units.Length - 1) {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0? "0" : "0.##") + " " + units[unit];
        }
        private void BuildCharacterInformationPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Character Information", "View basic information for a character in the loaded save."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                Border lockedCard = CreateCard();
                StackPanel lockedStack = new StackPanel();
                lockedStack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
                lockedStack.Children.Add(CreateFieldLabel("Load a save first from Save > Save Import."));
                lockedCard.Child = lockedStack;
                root.Children.Add(lockedCard);
                ContentHost.Children.Add(root);
                return;
            }
            List<CharacterSlotInfo> slots = ReadCharacterSlots(loadedSavePath).Where(slot =>!slot.IsEmpty).ToList();
            if (slots.Count == 0) {
                Border emptyCard = CreateCard();
                StackPanel emptyStack = new StackPanel();
                emptyStack.Children.Add(CreateSectionTitle("NO CHARACTERS"));
                emptyStack.Children.Add(CreateFieldLabel("No active character slots were found in this save."));
                emptyCard.Child = emptyStack;
                root.Children.Add(emptyCard);
                ContentHost.Children.Add(root);
                return;
            }
            if (selectedCharacterSlotIndex<0 ||!slots.Any(slot => slot.Index == selectedCharacterSlotIndex)) {
                selectedCharacterSlotIndex = slots[0].Index;
            }
            Border selectorCard = CreateCard();
            selectorCard.Margin = new Thickness(0, 0, 0, 14);
            StackPanel selectorStack = new StackPanel();
            selectorStack.Children.Add(CreateSectionTitle("CHARACTER"));
            ComboBox characterCombo = CreateNamedSlotCombo(slots, false);
            characterCombo.Width = 245;
            int selectedComboIndex = slots.FindIndex(slot => slot.Index == selectedCharacterSlotIndex);
            if (selectedComboIndex >= 0) {
                characterCombo.SelectedIndex = selectedComboIndex;
            }
            selectorStack.Children.Add(characterCombo);
            selectorCard.Child = selectorStack;
            root.Children.Add(selectorCard);
            Border infoCard = CreateCard();
            StackPanel infoStack = new StackPanel();
            infoStack.Children.Add(CreateSectionTitle("CHARACTER INFORMATION"));
            void RenderCharacterInfo() {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (selected == null) {
                    return;
                }
                selectedCharacterSlotIndex = selected.Index;
                CharacterProfileInfo profile = ReadCharacterProfileInfo(loadedSavePath, selected.Index);
                while (infoStack.Children.Count> 1) {
                    infoStack.Children.RemoveAt(1);
                }
                infoStack.Children.Add(CreateInfoLine("Name", profile.Name));
                infoStack.Children.Add(CreateInfoLine("Slot", (profile.SlotIndex + 1).ToString()));
                infoStack.Children.Add(CreateInfoLine("Level", profile.Level.ToString("N0")));
                infoStack.Children.Add(CreateInfoLine("Runes", profile.Runes.ToString("N0")));
                infoStack.Children.Add(CreateInfoLine("Play Time", FormatCharacterPlayTime(profile.SecondsPlayed)));
                infoStack.Children.Add(CreateInfoLine("Archetype", GetArchetypeName(profile.Archetype)));
                infoStack.Children.Add(CreateInfoLine("Body Type", GetBodyTypeName(profile.BodyType)));
                infoStack.Children.Add(CreateInfoLine("Save Account", Directory.GetParent(loadedSavePath)?.Name ?? "Unknown"));
                infoStack.Children.Add(CreateInfoLine("Save Type", Path.GetExtension(loadedSavePath).Equals(".co2", StringComparison.OrdinalIgnoreCase)? "Seamless Co-op" : "Original Game"));
            }
            characterCombo.SelectionChanged += (_, _) => {
                RenderCharacterInfo();
            };
            RenderCharacterInfo();
            infoCard.Child = infoStack;
            root.Children.Add(infoCard);
            ContentHost.Children.Add(root);
            StatusText.Text = "Character > Character Information";
        }
        private List<CharacterSlotInfo> GetEditableCharacterSlots() {
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                return new List<CharacterSlotInfo>();
            }
            return ReadCharacterSlots(loadedSavePath).Where(slot =>!slot.IsEmpty).ToList();
        }
        private ComboBox CreateCharacterEditorSlotCombo(List<CharacterSlotInfo> slots) {
            if (selectedCharacterSlotIndex<0 ||!slots.Any(slot => slot.Index == selectedCharacterSlotIndex)) {
                selectedCharacterSlotIndex = slots.Count> 0? slots[0].Index : -1;
            }
            ComboBox combo = CreateNamedSlotCombo(slots, false);
            combo.Width = 280;
            int selectedIndex = slots.FindIndex(slot => slot.Index == selectedCharacterSlotIndex);
            if (selectedIndex >= 0) {
                combo.SelectedIndex = selectedIndex;
            }
            return combo;
        }
        private Border BuildCharacterEditorSelectorCard(ComboBox combo) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("CHARACTER"));
            stack.Children.Add(combo);
            card.Child = stack;
            return card;
        }
        private TextBox CreateCharacterNumericTextBox(string value, double width = 120) {
            TextBox box = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Text = value, Width = width, Height = 28, Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center
            };
            box.PreviewTextInput += (_, e) => {
                e.Handled = e.Text.Any(ch =>!char.IsDigit(ch));
            };
            DataObject.AddPastingHandler(box, (_, e) => {
                if (e.DataObject.GetDataPresent(DataFormats.Text)) {
                    string pasted = e.DataObject.GetData(DataFormats.Text)?.ToString() ?? ""; if (pasted.Any(ch =>!char.IsDigit(ch))) {
                        e.CancelCommand();
                    }
                }
            });
            return box;
        }
        private Grid CreateCharacterEditorRow(string label, UIElement editor) {
            Grid row = new Grid {
                Margin = new Thickness(0, 6, 0, 0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(170)
            });
            row.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            TextBlock labelText = new TextBlock {
                Text = label, Foreground = GetResourceBrush("TextSoft"), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);
            return row;
        }
        private Button CreateCharacterApplyButton(string text) {
            Button button = new Button {
                Content = text, Style = (Style) FindResource("ActionButton"), MinWidth = 0, Width = 124, Height = 30, Padding = new Thickness(12, 3, 12, 3), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0)
            };
            return button;
        }
        private void AddCharacterEditorLockedCard(StackPanel root) {
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
            stack.Children.Add(CreateFieldLabel("Load a save first from Save > Save Import."));
            card.Child = stack;
            root.Children.Add(card);
        }
        private void AddNoCharacterEditorCard(StackPanel root) {
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("NO CHARACTERS"));
            stack.Children.Add(CreateFieldLabel("No active character slots were found in this save."));
            card.Child = stack;
            root.Children.Add(card);
        }
        private void BuildCharacterStatsPage() {
            try {
                BuildCharacterStatsPageCore();
            } catch (Exception ex) {
                try {
                    ContentHost.Children.Clear();
                    StackPanel root = new StackPanel();
                    root.Children.Add(CreatePageTitle("Stats", "Edit the eight character attributes. Applying stats automatically recalculates the character level."));
                    Border card = CreateCard();
                    StackPanel stack = new StackPanel();
                    stack.Children.Add(CreateSectionTitle("STATS UNAVAILABLE"));
                    stack.Children.Add(CreateFieldLabel("Could not locate or read PlayerGameData for the selected character. Stats controls have been disabled to protect the save."));
                    card.Child = stack;
                    root.Children.Add(card);
                    ContentHost.Children.Add(root);
                    StatusText.Text = "Character > Stats";
                    ShowToast("Could not locate PlayerGameData • " + ex.Message, ToastKind.Warning);
                } catch {
                    try {
                        StatusText.Text = "Character > Stats unavailable";
                    } catch {
                    }
                }
            }
        }
        private void BuildCharacterStatsPageCore() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Stats", "Edit the eight character attributes. Applying stats automatically recalculates the character level."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                AddCharacterEditorLockedCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Stats";
                return;
            }
            List<CharacterSlotInfo> slots = GetEditableCharacterSlots();
            if (slots.Count == 0) {
                AddNoCharacterEditorCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Stats";
                return;
            }
            ComboBox combo = CreateCharacterEditorSlotCombo(slots);
            root.Children.Add(BuildCharacterEditorSelectorCard(combo));
            Border editorCard = CreateCard();
            StackPanel editorStack = new StackPanel();
            editorStack.Children.Add(CreateSectionTitle("ATTRIBUTES"));
            TextBlock levelPreview = CreateFieldLabel("Calculated level: -");
            Dictionary<string, TextBox> boxes = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
            string[] statNames = {
                "Vigor", "Mind", "Endurance", "Strength", "Dexterity", "Intelligence", "Faith", "Arcane"
            };
            foreach (string statName in statNames) {
                TextBox box = CreateCharacterNumericTextBox("0", 112);
                boxes[statName] = box;
                editorStack.Children.Add(CreateCharacterEditorRow(statName, box));
                box.TextChanged += (_, _) => {
                    if (TryReadStatsFromBoxes(boxes, out CharacterStatsInfo previewStats, false)) {
                        uint calculated = CalculateLevelFromStats(previewStats);
                        levelPreview.Text = "Calculated level: " + calculated.ToString("N0");
                    } else {
                        levelPreview.Text = "Calculated level: -";
                    }
                };
            }
            levelPreview.Margin = new Thickness(0, 14, 0, 0);
            editorStack.Children.Add(levelPreview);
            TextBlock note = CreateFieldLabel("Valid range: 1–99 per attribute. A safety backup is created before the first Character edit in this session.");
            note.Margin = new Thickness(0, 6, 0, 0);
            editorStack.Children.Add(note);
            Button applyButton = CreateCharacterApplyButton("Apply Stats");
            editorStack.Children.Add(applyButton);
            editorCard.Child = editorStack;
            root.Children.Add(editorCard);
            void LoadSelectedStats() {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    return;
                }
                selectedCharacterSlotIndex = selected.Index;
                CharacterStatsInfo stats;
                try {
                    stats = ReadCharacterStatsInfo(loadedSavePath, selected.Index);
                } catch (Exception ex) {
                    levelPreview.Text = "Calculated level: unavailable";
                    foreach (TextBox statBox in boxes.Values) {
                        statBox.Text = "";
                        statBox.IsEnabled = false;
                    }
                    applyButton.IsEnabled = false;
                    ShowToast("Could not read character stats • " + ex.Message, ToastKind.Warning);
                    return;
                }
                foreach (TextBox statBox in boxes.Values) {
                    statBox.IsEnabled = true;
                }
                applyButton.IsEnabled = true;
                boxes["Vigor"].Text = stats.Vigor.ToString();
                boxes["Mind"].Text = stats.Mind.ToString();
                boxes["Endurance"].Text = stats.Endurance.ToString();
                boxes["Strength"].Text = stats.Strength.ToString();
                boxes["Dexterity"].Text = stats.Dexterity.ToString();
                boxes["Intelligence"].Text = stats.Intelligence.ToString();
                boxes["Faith"].Text = stats.Faith.ToString();
                boxes["Arcane"].Text = stats.Arcane.ToString();
                levelPreview.Text = "Calculated level: " + CalculateLevelFromStats(stats).ToString("N0");
            }
            combo.SelectionChanged += (_, _) => {
                LoadSelectedStats();
            };
            applyButton.Click += (_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    ShowToast("Select a character first", ToastKind.Info);
                    return;
                }
                if (!TryReadStatsFromBoxes(boxes, out CharacterStatsInfo stats, true)) {
                    ShowToast("Each stat must be a whole number from 1 to 99", ToastKind.Warning);
                    return;
                }
                try {
                    uint newLevel = WriteCharacterStats(loadedSavePath, selected.Index, stats);
                    ShowToast("Stats updated • Level " + newLevel.ToString("N0"), ToastKind.Success);
                    BuildCharacterStatsPage();
                } catch (Exception ex) {
                    ShowToast("Could not update stats • " + ex.Message, ToastKind.Warning);
                }
            };
            LoadSelectedStats();
            ContentHost.Children.Add(root);
            StatusText.Text = "Character > Stats";
        }
        private void BuildCharacterLevelPage() {
            try {
                BuildCharacterLevelPageCore();
            } catch (Exception ex) {
                try {
                    ContentHost.Children.Clear();
                    StackPanel root = new StackPanel();
                    root.Children.Add(CreatePageTitle("Level", "Edit the displayed character level without changing individual attributes."));
                    Border card = CreateCard();
                    StackPanel stack = new StackPanel();
                    stack.Children.Add(CreateSectionTitle("LEVEL UNAVAILABLE"));
                    stack.Children.Add(CreateFieldLabel("Could not read the selected character data. Controls have been disabled to protect the save."));
                    card.Child = stack;
                    root.Children.Add(card);
                    ContentHost.Children.Add(root);
                    StatusText.Text = "Character > Level";
                    ShowToast("Could not open level editor • " + ex.Message, ToastKind.Warning);
                } catch {
                    try {
                        StatusText.Text = "Character > Level unavailable";
                    } catch {
                    }
                }
            }
        }
        private void BuildCharacterLevelPageCore() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Level", "Edit the displayed character level without changing individual attributes."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                AddCharacterEditorLockedCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Level";
                return;
            }
            List<CharacterSlotInfo> slots = GetEditableCharacterSlots();
            if (slots.Count == 0) {
                AddNoCharacterEditorCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Level";
                return;
            }
            ComboBox combo = CreateCharacterEditorSlotCombo(slots);
            root.Children.Add(BuildCharacterEditorSelectorCard(combo));
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("LEVEL"));
            TextBlock currentLevel = CreateFieldLabel("Current level: -");
            stack.Children.Add(currentLevel);
            TextBox levelBox = CreateCharacterNumericTextBox("1");
            stack.Children.Add(CreateCharacterEditorRow("New Level", levelBox));
            TextBlock warning = CreateFieldLabel("Manual level editing does not redistribute attributes. Use Stats if you want level to follow the eight attributes automatically.");
            warning.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(warning);
            Button apply = CreateCharacterApplyButton("Set Level");
            stack.Children.Add(apply);
            card.Child = stack;
            root.Children.Add(card);
            void LoadSelectedLevel() {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    return;
                }
                selectedCharacterSlotIndex = selected.Index;
                try {
                    CharacterProfileInfo profile = ReadCharacterProfileInfo(loadedSavePath, selected.Index, false);
                    currentLevel.Text = "Current level: " + profile.Level.ToString("N0");
                    levelBox.Text = profile.Level.ToString();
                    levelBox.IsEnabled = true;
                    apply.IsEnabled = true;
                } catch (Exception ex) {
                    currentLevel.Text = "Current level: unavailable";
                    levelBox.Text = "";
                    levelBox.IsEnabled = false;
                    apply.IsEnabled = false;
                    ShowToast("Could not read character level • " + ex.Message, ToastKind.Warning);
                }
            }
            combo.SelectionChanged += (_, _) => {
                LoadSelectedLevel();
            };
            apply.Click += (_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    return;
                }
                if (!uint.TryParse(levelBox.Text, out uint newLevel) || newLevel<1 || newLevel> 713) {
                    ShowToast("Level must be from 1 to 713", ToastKind.Warning);
                    return;
                }
                try {
                    WriteCharacterLevel(loadedSavePath, selected.Index, newLevel);
                    ShowToast("Level updated • " + newLevel.ToString("N0"), ToastKind.Success);
                    BuildCharacterLevelPage();
                } catch (Exception ex) {
                    ShowToast("Could not update level • " + ex.Message, ToastKind.Warning);
                }
            };
            LoadSelectedLevel();
            ContentHost.Children.Add(root);
            StatusText.Text = "Character > Level";
        }
        private void BuildCharacterRunesPage() {
            try {
                BuildCharacterRunesPageCore();
            } catch (Exception ex) {
                try {
                    ContentHost.Children.Clear();
                    StackPanel root = new StackPanel();
                    root.Children.Add(CreatePageTitle("Runes", "Edit the number of runes currently held by the selected character."));
                    Border card = CreateCard();
                    StackPanel stack = new StackPanel();
                    stack.Children.Add(CreateSectionTitle("RUNES UNAVAILABLE"));
                    stack.Children.Add(CreateFieldLabel("Could not read the selected character data. Controls have been disabled to protect the save."));
                    card.Child = stack;
                    root.Children.Add(card);
                    ContentHost.Children.Add(root);
                    StatusText.Text = "Character > Runes";
                    ShowToast("Could not open runes editor • " + ex.Message, ToastKind.Warning);
                } catch {
                    try {
                        StatusText.Text = "Character > Runes unavailable";
                    } catch {
                    }
                }
            }
        }
        private void BuildCharacterRunesPageCore() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Runes", "Edit the number of runes currently held by the selected character."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                AddCharacterEditorLockedCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Runes";
                return;
            }
            List<CharacterSlotInfo> slots = GetEditableCharacterSlots();
            if (slots.Count == 0) {
                AddNoCharacterEditorCard(root);
                ContentHost.Children.Add(root);
                StatusText.Text = "Character > Runes";
                return;
            }
            ComboBox combo = CreateCharacterEditorSlotCombo(slots);
            root.Children.Add(BuildCharacterEditorSelectorCard(combo));
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("RUNES"));
            TextBlock currentRunes = CreateFieldLabel("Current runes: -");
            stack.Children.Add(currentRunes);
            TextBox runesBox = CreateCharacterNumericTextBox("0", 150);
            stack.Children.Add(CreateCharacterEditorRow("New Runes", runesBox));
            TextBlock limit = CreateFieldLabel("Range: 0–999,999,999.");
            limit.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(limit);
            Button maxButton = new Button {
                Content = "Set 999,999,999", Style = (Style) FindResource("ActionButton"), MinWidth = 0, Width = 142, Height = 30, Padding = new Thickness(12, 3, 12, 3), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0)
            };
            maxButton.Click += (_, _) => {
                runesBox.Text = "999999999";
            };
            stack.Children.Add(maxButton);
            Button apply = CreateCharacterApplyButton("Apply Runes");
            stack.Children.Add(apply);
            card.Child = stack;
            root.Children.Add(card);
            void LoadSelectedRunes() {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    return;
                }
                selectedCharacterSlotIndex = selected.Index;
                try {
                    CharacterProfileInfo profile = ReadCharacterProfileInfo(loadedSavePath, selected.Index);
                    currentRunes.Text = "Current runes: " + profile.Runes.ToString("N0");
                    runesBox.Text = profile.Runes.ToString();
                    runesBox.IsEnabled = true;
                    maxButton.IsEnabled = true;
                    apply.IsEnabled = true;
                } catch (Exception ex) {
                    currentRunes.Text = "Current runes: unavailable";
                    runesBox.Text = "";
                    runesBox.IsEnabled = false;
                    maxButton.IsEnabled = false;
                    apply.IsEnabled = false;
                    ShowToast("Could not locate PlayerGameData • " + ex.Message, ToastKind.Warning);
                }
            }
            combo.SelectionChanged += (_, _) => {
                LoadSelectedRunes();
            };
            apply.Click += (_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                if (selected == null) {
                    return;
                }
                if (!uint.TryParse(runesBox.Text, out uint runes) || runes> 999999999) {
                    ShowToast("Runes must be from 0 to 999,999,999", ToastKind.Warning);
                    return;
                }
                try {
                    WriteCharacterRunes(loadedSavePath, selected.Index, runes);
                    ShowToast("Runes updated • " + runes.ToString("N0"), ToastKind.Success);
                    BuildCharacterRunesPage();
                } catch (Exception ex) {
                    ShowToast("Could not update runes • " + ex.Message, ToastKind.Warning);
                }
            };
            LoadSelectedRunes();
            ContentHost.Children.Add(root);
            StatusText.Text = "Character > Runes";
        }
        private CharacterStatsInfo ReadCharacterStatsInfo(string savePath, int slotIndex) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            CharacterProfileInfo profile = ReadCharacterProfileInfo(savePath, slotIndex, false);
            int playerDataOffset = FindPlayerGameDataOffset(data, slotIndex, profile.Level, profile.Name);
            return new CharacterStatsInfo {
                Vigor = BitConverter.ToUInt32(data, playerDataOffset - 379), Mind = BitConverter.ToUInt32(data, playerDataOffset - 375), Endurance = BitConverter.ToUInt32(data, playerDataOffset - 371), Strength = BitConverter.ToUInt32(data, playerDataOffset - 367), Dexterity = BitConverter.ToUInt32(data, playerDataOffset - 363), Intelligence = BitConverter.ToUInt32(data, playerDataOffset - 359), Faith = BitConverter.ToUInt32(data, playerDataOffset - 355), Arcane = BitConverter.ToUInt32(data, playerDataOffset - 351)
            };
        }
        private static bool TryReadStatsFromBoxes(Dictionary<string, TextBox> boxes, out CharacterStatsInfo stats, bool enforceRange) {
            stats = new CharacterStatsInfo();
            string[] names = {
                "Vigor", "Mind", "Endurance", "Strength", "Dexterity", "Intelligence", "Faith", "Arcane"
            };
            uint[] values = new uint[names.Length];
            for (int i = 0; i<names.Length; i++) {
                if (!boxes.TryGetValue(names[i], out TextBox? box) ||!uint.TryParse(box.Text, out uint value)) {
                    return false;
                }
                if (enforceRange && (value<1 || value> 99)) {
                    return false;
                }
                values[i] = value;
            }
            stats.Vigor = values[0];
            stats.Mind = values[1];
            stats.Endurance = values[2];
            stats.Strength = values[3];
            stats.Dexterity = values[4];
            stats.Intelligence = values[5];
            stats.Faith = values[6];
            stats.Arcane = values[7];
            return true;
        }
        private static uint CalculateLevelFromStats(CharacterStatsInfo stats) {
            ulong total = stats.Vigor + stats.Mind + stats.Endurance + stats.Strength + stats.Dexterity + stats.Intelligence + stats.Faith + stats.Arcane;
            if (total <= 79) {
                return 1;
            }
            ulong level = total - 79;
            return level> uint.MaxValue? uint.MaxValue : (uint) level;
        }
        private uint WriteCharacterStats(string savePath, int slotIndex, CharacterStatsInfo stats) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            CharacterProfileInfo profile = ReadCharacterProfileInfo(savePath, slotIndex, false);
            int playerDataOffset = FindPlayerGameDataOffset(data, slotIndex, profile.Level, profile.Name);
            WriteUInt32(data, playerDataOffset - 379, stats.Vigor);
            WriteUInt32(data, playerDataOffset - 375, stats.Mind);
            WriteUInt32(data, playerDataOffset - 371, stats.Endurance);
            WriteUInt32(data, playerDataOffset - 367, stats.Strength);
            WriteUInt32(data, playerDataOffset - 363, stats.Dexterity);
            WriteUInt32(data, playerDataOffset - 359, stats.Intelligence);
            WriteUInt32(data, playerDataOffset - 355, stats.Faith);
            WriteUInt32(data, playerDataOffset - 351, stats.Arcane);
            uint level = CalculateLevelFromStats(stats);
            WriteUInt32(data, playerDataOffset - 335, level);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            WriteUInt32(data, profileOffset + 0x22, level);
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
            return level;
        }
        private void WriteCharacterLevel(string savePath, int slotIndex, uint newLevel) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            CharacterProfileInfo profile = ReadCharacterProfileInfo(savePath, slotIndex, false);
            int playerDataOffset = FindPlayerGameDataOffset(data, slotIndex, profile.Level, profile.Name);
            WriteUInt32(data, playerDataOffset - 335, newLevel);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            WriteUInt32(data, profileOffset + 0x22, newLevel);
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private void WriteCharacterRunes(string savePath, int slotIndex, uint runes) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            CharacterProfileInfo profile = ReadCharacterProfileInfo(savePath, slotIndex, false);
            int playerDataOffset = FindPlayerGameDataOffset(data, slotIndex, profile.Level, profile.Name);
            WriteUInt32(data, playerDataOffset - 331, runes);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            WriteUInt32(data, profileOffset + 0x2A, runes);
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private static void WriteUInt32(byte[] data, int offset, uint value) {
            if (offset<0 || offset + 4> data.Length) {
                throw new InvalidDataException("Character data offset is outside the save file.");
            }
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, data, offset, 4);
        }
        private const int MirrorPresetRelativeOffset = 0x154;
        private const int MirrorPresetSlotSize = 0x130;
        private const int MirrorPresetSlotCount = 15;
        private const int MirrorPresetBlockSize = MirrorPresetSlotSize * MirrorPresetSlotCount;
        private void BuildExportPresetPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Export Preset", "Export one slider preset slot, or export all 15 Mirror Favorites presets for transfer to another save."));
            if (!TryGetAppearancePresetSave(root, "Presets > Export Preset")) return;
            List<MirrorPresetSlotInfo> slots;
            try {
                slots = ReadMirrorPresetSlots(loadedSavePath);
            } catch (Exception ex) {
                AddAppearancePresetUnavailableCard(root, "Could not read appearance presets • " + ex.Message);
                ContentHost.Children.Add(root);
                StatusText.Text = "Presets > Export Preset";
                return;
            }
            Border singleCard = CreateCard();
            singleCard.Margin = new Thickness(0, 0, 0, 14);
            StackPanel singleStack = new StackPanel();
            singleStack.Children.Add(CreateSectionTitle("SINGLE PRESET SLOT"));
            singleStack.Children.Add(CreateFieldLabel("Export one non-empty Mirror Favorites slider preset."));
            ComboBox singleCombo = CreateMirrorPresetCombo(slots, false);
            singleCombo.Margin = new Thickness(0, 10, 0, 12);
            singleStack.Children.Add(singleCombo);
            TextBlock singlePreview = CreateFieldLabel("Select a preset slot.");
            singlePreview.Margin = new Thickness(0, 0, 0, 12);
            singleStack.Children.Add(singlePreview);
            Button singleExportButton = CreatePresetActionButton("Export Selected Preset...");
            singleStack.Children.Add(singleExportButton);
            singleCard.Child = singleStack;
            root.Children.Add(singleCard);
            Border fullCard = CreateCard();
            StackPanel fullStack = new StackPanel();
            fullStack.Children.Add(CreateSectionTitle("FULL PRESETS — ALL 15 SLOTS"));
            fullStack.Children.Add(CreateFieldLabel("Export the complete Mirror Favorites preset block, including occupied and empty slots."));
            TextBlock fullNote = CreatePresetNote("NOTE: Full Presets is intended for copying the entire 15-slot preset collection to another Elden Ring save.");
            fullNote.Margin = new Thickness(0, 8, 0, 12);
            fullStack.Children.Add(fullNote);
            Button fullExportButton = CreatePresetActionButton("Export All 15 Presets...");
            fullStack.Children.Add(fullExportButton);
            fullCard.Child = fullStack;
            root.Children.Add(fullCard);
            void RefreshSinglePreview() {
                MirrorPresetSlotInfo? item = singleCombo.SelectedItem as MirrorPresetSlotInfo;
                singlePreview.Text = item == null? "No non-empty preset slots were found." : FormatMirrorPresetPreview(item);
                singleExportButton.IsEnabled = item != null &&!item.IsEmpty;
            }
            singleCombo.SelectionChanged += (_, _) => RefreshSinglePreview();
            singleExportButton.Click += (_, _) => {
                if (singleCombo.SelectedItem is not MirrorPresetSlotInfo item || item.IsEmpty) return;
                SaveFileDialog dialog = new SaveFileDialog {
                    Title = "Export Single Appearance Preset", Filter = "Elden Ring Toolkit Single Preset (*.erpreset)|*.erpreset", DefaultExt = ".erpreset", AddExtension = true, FileName = "Appearance Preset Slot " + (item.Index + 1) + ".erpreset"
                };
                if (dialog.ShowDialog() != true) return;
                try {
                    SaveAppearancePresetFile(item, dialog.FileName);
                    ShowToast("Preset slot " + (item.Index + 1) + " exported", ToastKind.Success);
                } catch (Exception ex) {
                    ShowToast("Could not export preset • " + ex.Message, ToastKind.Warning);
                }
            };
            fullExportButton.Click += (_, _) => {
                SaveFileDialog dialog = new SaveFileDialog {
                    Title = "Export All Appearance Presets", Filter = "Elden Ring Toolkit Full Presets (*.erpresetpack)|*.erpresetpack", DefaultExt = ".erpresetpack", AddExtension = true, FileName = "Appearance Presets - All 15 Slots.erpresetpack"
                };
                if (dialog.ShowDialog() != true) return;
                try {
                    SaveAppearancePresetPackFile(loadedSavePath, dialog.FileName);
                    ShowToast("All 15 appearance preset slots exported", ToastKind.Success);
                } catch (Exception ex) {
                    ShowToast("Could not export full presets • " + ex.Message, ToastKind.Warning);
                }
            };
            RefreshSinglePreview();
            ContentHost.Children.Add(root);
            StatusText.Text = "Presets > Export Preset";
        }
        private void BuildImportPresetPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Import Preset", "Import one slider preset into any Mirror Favorites slot, or replace all 15 preset slots from a Full Presets file."));
            if (!TryGetAppearancePresetSave(root, "Presets > Import Preset")) return;
            List<MirrorPresetSlotInfo> slots;
            try {
                slots = ReadMirrorPresetSlots(loadedSavePath);
            } catch (Exception ex) {
                AddAppearancePresetUnavailableCard(root, "Could not read appearance presets • " + ex.Message);
                ContentHost.Children.Add(root);
                StatusText.Text = "Presets > Import Preset";
                return;
            }
            Border singleCard = CreateCard();
            singleCard.Margin = new Thickness(0, 0, 0, 14);
            StackPanel singleStack = new StackPanel();
            singleStack.Children.Add(CreateSectionTitle("SINGLE PRESET SLOT"));
            TextBlock singleNote = CreatePresetNote("NOTE: A single preset can be imported into an empty slot or overwrite an occupied slot. Overwriting replaces only the selected slot.");
            singleNote.Margin = new Thickness(0, 2, 0, 12);
            singleStack.Children.Add(singleNote);
            Button browseSingleButton = CreatePresetActionButton("Browse Single Preset...");
            singleStack.Children.Add(browseSingleButton);
            TextBlock singleFileText = CreateFieldLabel("No single preset selected.");
            singleFileText.Margin = new Thickness(0, 8, 0, 8);
            singleStack.Children.Add(singleFileText);
            TextBlock singlePreview = CreateFieldLabel("Preset preview will appear here.");
            singlePreview.Margin = new Thickness(0, 0, 0, 12);
            singleStack.Children.Add(singlePreview);
            singleStack.Children.Add(CreateFieldLabel("Target Mirror Favorites slot"));
            ComboBox targetCombo = CreateMirrorPresetCombo(slots, true);
            targetCombo.Margin = new Thickness(0, 8, 0, 12);
            singleStack.Children.Add(targetCombo);
            Button importSingleButton = CreatePresetActionButton("Import Selected Preset to Slot");
            importSingleButton.IsEnabled = false;
            singleStack.Children.Add(importSingleButton);
            singleCard.Child = singleStack;
            root.Children.Add(singleCard);
            Border fullCard = CreateCard();
            StackPanel fullStack = new StackPanel();
            fullStack.Children.Add(CreateSectionTitle("FULL PRESETS — ALL 15 SLOTS"));
            TextBlock fullNote = CreatePresetNote("NOTE: Importing Full Presets replaces all 15 Mirror Favorites slots in the loaded save, including empty-slot state. A safety backup is created first.");
            fullNote.Margin = new Thickness(0, 2, 0, 12);
            fullStack.Children.Add(fullNote);
            Button browseFullButton = CreatePresetActionButton("Browse Full Presets...");
            fullStack.Children.Add(browseFullButton);
            TextBlock fullFileText = CreateFieldLabel("No Full Presets file selected.");
            fullFileText.Margin = new Thickness(0, 8, 0, 8);
            fullStack.Children.Add(fullFileText);
            TextBlock fullPreview = CreateFieldLabel("Full Presets preview will appear here.");
            fullPreview.Margin = new Thickness(0, 0, 0, 12);
            fullStack.Children.Add(fullPreview);
            Button importFullButton = CreatePresetActionButton("Import All 15 Presets to Save");
            importFullButton.IsEnabled = false;
            fullStack.Children.Add(importFullButton);
            fullCard.Child = fullStack;
            root.Children.Add(fullCard);
            AppearancePresetFileData? loadedSingle = null;
            AppearancePresetPackFileData? loadedFull = null;
            browseSingleButton.Click += (_, _) => {
                OpenFileDialog dialog = new OpenFileDialog {
                    Title = "Import Single Appearance Preset", Filter = "Elden Ring Toolkit Single Preset (*.erpreset)|*.erpreset|JSON files (*.json)|*.json|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true) return;
                try {
                    loadedSingle = LoadAppearancePresetFile(dialog.FileName);
                    singleFileText.Text = Path.GetFileName(dialog.FileName);
                    singlePreview.Text = FormatAppearancePresetFilePreview(loadedSingle);
                    importSingleButton.IsEnabled = targetCombo.SelectedItem != null;
                } catch (Exception ex) {
                    loadedSingle = null;
                    importSingleButton.IsEnabled = false;
                    singleFileText.Text = "No single preset selected.";
                    singlePreview.Text = "Preset could not be loaded.";
                    ShowToast("Could not load single preset • " + ex.Message, ToastKind.Warning);
                }
            };
            targetCombo.SelectionChanged += (_, _) => {
                importSingleButton.IsEnabled = loadedSingle != null && targetCombo.SelectedItem != null;
            };
            importSingleButton.Click += (_, _) => {
                if (loadedSingle == null || targetCombo.SelectedItem is not MirrorPresetSlotInfo target) return;
                if (!target.IsEmpty) {
                    MessageBoxResult overwriteResult = MessageBox.Show("Slot " + (target.Index + 1) + " already contains a preset. Overwrite this slot?", "Overwrite Preset Slot", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (overwriteResult != MessageBoxResult.Yes) return;
                }
                try {
                    WriteAppearancePresetToMirrorSlot(loadedSavePath, target.Index, loadedSingle);
                    ShowToast("Preset imported to slot " + (target.Index + 1), ToastKind.Success);
                    BuildImportPresetPage();
                } catch (Exception ex) {
                    ShowToast("Could not write preset • " + ex.Message, ToastKind.Warning);
                }
            };
            browseFullButton.Click += (_, _) => {
                OpenFileDialog dialog = new OpenFileDialog {
                    Title = "Import Full Appearance Presets", Filter = "Elden Ring Toolkit Full Presets (*.erpresetpack)|*.erpresetpack|JSON files (*.json)|*.json|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true) return;
                try {
                    loadedFull = LoadAppearancePresetPackFile(dialog.FileName);
                    fullFileText.Text = Path.GetFileName(dialog.FileName);
                    fullPreview.Text = FormatAppearancePresetPackPreview(loadedFull);
                    importFullButton.IsEnabled = true;
                } catch (Exception ex) {
                    loadedFull = null;
                    importFullButton.IsEnabled = false;
                    fullFileText.Text = "No Full Presets file selected.";
                    fullPreview.Text = "Full Presets file could not be loaded.";
                    ShowToast("Could not load full presets • " + ex.Message, ToastKind.Warning);
                }
            };
            importFullButton.Click += (_, _) => {
                if (loadedFull == null) return;
                MessageBoxResult result = MessageBox.Show("Replace all 15 Mirror Favorites preset slots in the loaded save?\n\nThis includes occupied and empty slots. A safety backup will be created first.", "Import Full Presets", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
                try {
                    WriteAppearancePresetPackToSave(loadedSavePath, loadedFull);
                    ShowToast("All 15 appearance preset slots imported", ToastKind.Success);
                    BuildImportPresetPage();
                } catch (Exception ex) {
                    ShowToast("Could not import full presets • " + ex.Message, ToastKind.Warning);
                }
            };
            ContentHost.Children.Add(root);
            StatusText.Text = "Presets > Import Preset";
        }
        private void BuildDeleteClearPresetPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Delete / Clear Preset", "Clear one Mirror Favorites appearance / slider preset slot from the loaded save."));
            if (!TryGetAppearancePresetSave(root, "Presets > Delete / Clear Preset")) return;
            List<MirrorPresetSlotInfo> slots;
            try {
                slots = ReadMirrorPresetSlots(loadedSavePath);
            } catch (Exception ex) {
                AddAppearancePresetUnavailableCard(root, "Could not read appearance presets • " + ex.Message);
                ContentHost.Children.Add(root);
                StatusText.Text = "Presets > Delete / Clear Preset";
                return;
            }
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("CLEAR MIRROR FAVORITES SLOT"));
            TextBlock note = CreatePresetNote("NOTE: Clearing removes only the selected preset slot from this save. It does not change the character's current appearance.");
            note.Margin = new Thickness(0, 2, 0, 12);
            stack.Children.Add(note);
            ComboBox combo = CreateMirrorPresetCombo(slots, true);
            combo.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(combo);
            TextBlock preview = CreateFieldLabel("Select a slot.");
            preview.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(preview);
            Button clearButton = CreatePresetActionButton("Delete / Clear Selected Slot");
            stack.Children.Add(clearButton);
            card.Child = stack;
            root.Children.Add(card);
            void RefreshPreview() {
                MirrorPresetSlotInfo? item = combo.SelectedItem as MirrorPresetSlotInfo;
                preview.Text = item == null? "Select a slot." : FormatMirrorPresetPreview(item);
                clearButton.IsEnabled = item != null &&!item.IsEmpty;
            }
            combo.SelectionChanged += (_, _) => RefreshPreview();
            clearButton.Click += (_, _) => {
                if (combo.SelectedItem is not MirrorPresetSlotInfo item || item.IsEmpty) return;
                MessageBoxResult result = MessageBox.Show("Delete / clear appearance preset slot " + (item.Index + 1) + "?", "Clear Preset Slot", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
                try {
                    ClearMirrorPresetSlot(loadedSavePath, item.Index);
                    ShowToast("Preset slot " + (item.Index + 1) + " cleared", ToastKind.Success);
                    BuildDeleteClearPresetPage();
                } catch (Exception ex) {
                    ShowToast("Could not clear preset • " + ex.Message, ToastKind.Warning);
                }
            };
            RefreshPreview();
            ContentHost.Children.Add(root);
            StatusText.Text = "Presets > Delete / Clear Preset";
        }
        private bool TryGetAppearancePresetSave(StackPanel root, string statusText) {
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                Border card = CreateCard();
                StackPanel stack = new StackPanel();
                stack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
                stack.Children.Add(CreateFieldLabel("Load an Elden Ring PC save first. Appearance presets live inside UserData10 of that save."));
                card.Child = stack;
                root.Children.Add(card);
                ContentHost.Children.Add(root);
                StatusText.Text = statusText;
                return false;
            }
            return true;
        }
        private void AddAppearancePresetUnavailableCard(StackPanel root, string message) {
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("PRESETS UNAVAILABLE"));
            stack.Children.Add(CreateFieldLabel(message));
            card.Child = stack;
            root.Children.Add(card);
        }
        private TextBlock CreatePresetNote(string text) {
            return new TextBlock {
                Text = text, Foreground = GetResourceBrush("AccentBright"), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
            };
        }
        private ComboBox CreateMirrorPresetCombo(List<MirrorPresetSlotInfo> slots, bool includeEmpty) {
            ComboBox combo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 520, HorizontalAlignment = HorizontalAlignment.Left
            };
            foreach (MirrorPresetSlotInfo slot in slots) {
                if (includeEmpty ||!slot.IsEmpty) combo.Items.Add(slot);
            }
            combo.DisplayMemberPath = nameof(MirrorPresetSlotInfo.DisplayName);
            if (combo.Items.Count> 0) combo.SelectedIndex = 0;
            return combo;
        }
        private List<MirrorPresetSlotInfo> ReadMirrorPresetSlots(string savePath) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            List<MirrorPresetSlotInfo> result = new List<MirrorPresetSlotInfo>();
            for (int index = 0; index<MirrorPresetSlotCount; index++) {
                int offset = GetMirrorPresetAbsoluteOffset(index);
                byte[] raw = new byte[MirrorPresetSlotSize];
                Buffer.BlockCopy(data, offset, raw, 0, raw.Length);
                bool isEmpty = IsMirrorPresetRawEmpty(raw);
                MirrorPresetSlotInfo slot = CreateMirrorPresetSlotInfo(index, raw, isEmpty);
                result.Add(slot);
            }
            return result;
        }
        private static bool IsMirrorPresetRawEmpty(byte[] raw) {
            if (raw.Length<MirrorPresetSlotSize) return true;
            int marker = BitConverter.ToInt32(raw, 0x14);
            bool faceMagic = raw[0x18] == (byte) 'F' && raw[0x19] == (byte) 'A' && raw[0x1A] == (byte) 'C' && raw[0x1B] == (byte) 'E';
            return marker == -1 ||!faceMagic;
        }
        private static MirrorPresetSlotInfo CreateMirrorPresetSlotInfo(int index, byte[] raw, bool isEmpty) {
            MirrorPresetSlotInfo slot = new MirrorPresetSlotInfo {
                Index = index, IsEmpty = isEmpty, BodyType = raw.Length> 0x09? raw[0x09] : (byte) 0, RawData = raw
            };
            if (!isEmpty && raw.Length >= MirrorPresetSlotSize) {
                slot.FaceModel = BitConverter.ToUInt32(raw, 0x24);
                slot.HairModel = BitConverter.ToUInt32(raw, 0x28);
                slot.EyeModel = BitConverter.ToUInt32(raw, 0x2C);
                slot.EyebrowModel = BitConverter.ToUInt32(raw, 0x30);
                slot.BeardModel = BitConverter.ToUInt32(raw, 0x34);
                slot.EyepatchModel = BitConverter.ToUInt32(raw, 0x38);
                slot.DecalModel = BitConverter.ToUInt32(raw, 0x3C);
                slot.EyelashModel = BitConverter.ToUInt32(raw, 0x40);
            }
            return slot;
        }
        private static string FormatMirrorPresetPreview(MirrorPresetSlotInfo slot) {
            if (slot.IsEmpty) return "Slot " + (slot.Index + 1) + " is empty.";
            return "Slot " + (slot.Index + 1) + " • " + GetMirrorBodyTypeText(slot.BodyType) + "\n" + "Face model: " + slot.FaceModel + " • Hair: " + slot.HairModel + " • Eyebrow: " + slot.EyebrowModel + "\n" + "Beard: " + slot.BeardModel + " • Eyepatch: " + slot.EyepatchModel + " • Tattoo/mark: " + slot.DecalModel + " • Eyelashes: " + slot.EyelashModel + "\n" + "Contains 64 face-shape sliders, 7 body-proportion sliders, and 91 skin/cosmetic values.";
        }
        private static string GetMirrorBodyTypeText(byte bodyType) {
            return bodyType == 0? "Body Type A" : "Body Type B";
        }
        private int GetMirrorPresetAbsoluteOffset(int slotIndex) {
            if (slotIndex<0 || slotIndex >= MirrorPresetSlotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return EldenRingUserData10DataOffset + MirrorPresetRelativeOffset + slotIndex * MirrorPresetSlotSize;
        }
        private void SaveAppearancePresetFile(MirrorPresetSlotInfo slot, string path) {
            if (slot.IsEmpty || slot.RawData.Length != MirrorPresetSlotSize) throw new InvalidDataException("The selected Mirror Favorites slot is empty or invalid.");
            AppearancePresetFileData preset = new AppearancePresetFileData {
                Format = "EldenRingToolkitAppearancePreset", Version = 1, Name = "Appearance Preset Slot " + (slot.Index + 1), CreatedUtc = DateTime.UtcNow, BodyType = slot.BodyType, DataBase64 = Convert.ToBase64String(slot.RawData)
            };
            WriteJsonFile(path, preset);
        }
        private void SaveAppearancePresetPackFile(string savePath, string path) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int firstOffset = GetMirrorPresetAbsoluteOffset(0);
            byte[] rawBlock = new byte[MirrorPresetBlockSize];
            Buffer.BlockCopy(data, firstOffset, rawBlock, 0, rawBlock.Length);
            AppearancePresetPackFileData pack = new AppearancePresetPackFileData {
                Format = "EldenRingToolkitAppearancePresetPack", Version = 1, Name = "Full Appearance Presets - 15 Slots", CreatedUtc = DateTime.UtcNow, SlotCount = MirrorPresetSlotCount, SlotSize = MirrorPresetSlotSize, DataBase64 = Convert.ToBase64String(rawBlock)
            };
            WriteJsonFile(path, pack);
        }
        private static void WriteJsonFile<T>(string path, T value) {
            string?directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions {
                WriteIndented = true
            }));
        }
        private AppearancePresetFileData LoadAppearancePresetFile(string path) {
            string json = File.ReadAllText(path);
            AppearancePresetFileData? preset = JsonSerializer.Deserialize<AppearancePresetFileData>(json);
            if (preset == null) throw new InvalidDataException("Preset file is empty or invalid.");
            ValidateAppearancePresetFile(preset);
            return preset;
        }
        private AppearancePresetPackFileData LoadAppearancePresetPackFile(string path) {
            string json = File.ReadAllText(path);
            AppearancePresetPackFileData? pack = JsonSerializer.Deserialize<AppearancePresetPackFileData>(json);
            if (pack == null) throw new InvalidDataException("Full Presets file is empty or invalid.");
            ValidateAppearancePresetPackFile(pack);
            return pack;
        }
        private static void ValidateAppearancePresetFile(AppearancePresetFileData preset) {
            if (!string.Equals(preset.Format, "EldenRingToolkitAppearancePreset", StringComparison.Ordinal)) throw new InvalidDataException("This is not an Elden Ring Toolkit single appearance preset.");
            byte[] raw = DecodePresetBase64(preset.DataBase64, "Preset appearance data");
            if (raw.Length != MirrorPresetSlotSize) throw new InvalidDataException("Single preset appearance data must be exactly 0x130 bytes.");
            ValidateActiveMirrorPresetRaw(raw);
        }
        private static void ValidateAppearancePresetPackFile(AppearancePresetPackFileData pack) {
            if (!string.Equals(pack.Format, "EldenRingToolkitAppearancePresetPack", StringComparison.Ordinal)) throw new InvalidDataException("This is not an Elden Ring Toolkit Full Presets file.");
            if (pack.SlotCount != MirrorPresetSlotCount || pack.SlotSize != MirrorPresetSlotSize) throw new InvalidDataException("Full Presets layout does not match the Toolkit 15-slot format.");
            byte[] rawBlock = DecodePresetBase64(pack.DataBase64, "Full Presets data");
            if (rawBlock.Length != MirrorPresetBlockSize) throw new InvalidDataException("Full Presets data has an unexpected size.");
            for (int index = 0; index<MirrorPresetSlotCount; index++) {
                byte[] slot = new byte[MirrorPresetSlotSize];
                Buffer.BlockCopy(rawBlock, index * MirrorPresetSlotSize, slot, 0, slot.Length);
                if (IsMirrorPresetRawEmpty(slot)) continue;
                ValidateActiveMirrorPresetRaw(slot);
            }
        }
        private static byte[] DecodePresetBase64(string?value, string description) {
            try {
                return Convert.FromBase64String(value ?? "");
            } catch (FormatException) {
                throw new InvalidDataException(description + " is not valid Base64.");
            }
        }
        private static void ValidateActiveMirrorPresetRaw(byte[] raw) {
            int marker = BitConverter.ToInt32(raw, 0x14);
            bool faceMagic = raw[0x18] == (byte) 'F' && raw[0x19] == (byte) 'A' && raw[0x1A] == (byte) 'C' && raw[0x1B] == (byte) 'E';
            if (marker != 0 ||!faceMagic) throw new InvalidDataException("Preset does not contain an active FACE appearance block.");
            if (BitConverter.ToUInt32(raw, 0x1C) != 4 || BitConverter.ToUInt32(raw, 0x20) != 0x120) throw new InvalidDataException("Preset FACE header is not valid.");
        }
        private static string FormatAppearancePresetFilePreview(AppearancePresetFileData preset) {
            ValidateAppearancePresetFile(preset);
            byte[] raw = DecodePresetBase64(preset.DataBase64, "Preset appearance data");
            return(string.IsNullOrWhiteSpace(preset.Name)? "Appearance Preset" : preset.Name) + " • " + GetMirrorBodyTypeText(raw[0x09]) + "\n" + "Face model: " + BitConverter.ToUInt32(raw, 0x24) + " • Hair: " + BitConverter.ToUInt32(raw, 0x28) + " • Eyebrow: " + BitConverter.ToUInt32(raw, 0x30) + "\n" + "64 face sliders • 7 body sliders • 91 skin/cosmetic values";
        }
        private static string FormatAppearancePresetPackPreview(AppearancePresetPackFileData pack) {
            ValidateAppearancePresetPackFile(pack);
            byte[] rawBlock = DecodePresetBase64(pack.DataBase64, "Full Presets data");
            int occupied = 0;
            for (int index = 0; index<MirrorPresetSlotCount; index++) {
                int slotOffset = index * MirrorPresetSlotSize;
                byte[] slot = new byte[MirrorPresetSlotSize];
                Buffer.BlockCopy(rawBlock, slotOffset, slot, 0, slot.Length);
                if (!IsMirrorPresetRawEmpty(slot)) occupied++;
            }
            return(string.IsNullOrWhiteSpace(pack.Name)? "Full Appearance Presets" : pack.Name) + "\n" + "15 slots total • " + occupied + " occupied • " + (MirrorPresetSlotCount - occupied) + " empty\n" + "Importing this file replaces the complete Mirror Favorites preset collection.";
        }
        private void WriteAppearancePresetToMirrorSlot(string savePath, int targetIndex, AppearancePresetFileData preset) {
            ValidateAppearancePresetFile(preset);
            byte[] raw = DecodePresetBase64(preset.DataBase64, "Preset appearance data");
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int offset = GetMirrorPresetAbsoluteOffset(targetIndex);
            Buffer.BlockCopy(raw, 0, data, offset, raw.Length);
            CreateEditorSafetyBackupOnce();
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private void WriteAppearancePresetPackToSave(string savePath, AppearancePresetPackFileData pack) {
            ValidateAppearancePresetPackFile(pack);
            byte[] rawBlock = DecodePresetBase64(pack.DataBase64, "Full Presets data");
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int firstOffset = GetMirrorPresetAbsoluteOffset(0);
            Buffer.BlockCopy(rawBlock, 0, data, firstOffset, rawBlock.Length);
            CreateEditorSafetyBackupOnce();
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private void ClearMirrorPresetSlot(string savePath, int targetIndex) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int offset = GetMirrorPresetAbsoluteOffset(targetIndex);
            byte[] marker = BitConverter.GetBytes( -1);
            Buffer.BlockCopy(marker, 0, data, offset + 0x14, marker.Length);
            CreateEditorSafetyBackupOnce();
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private Button CreatePresetActionButton(string text) {
            return new Button {
                Content = text, Style = (Style) FindResource("ActionButton"), MinWidth = 190, Margin = new Thickness(0, 0, 8, 8)
            };
        }
        private void BuildCharacterComingSoonPage(string title, string message) {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle(title, message));
            if (saveSessionLoaded && File.Exists(loadedSavePath)) {
                List<CharacterSlotInfo> slots = ReadCharacterSlots(loadedSavePath).Where(slot =>!slot.IsEmpty).ToList();
                if (slots.Count> 0) {
                    if (selectedCharacterSlotIndex<0 ||!slots.Any(slot => slot.Index == selectedCharacterSlotIndex)) {
                        selectedCharacterSlotIndex = slots[0].Index;
                    }
                    Border selectorCard = CreateCard();
                    StackPanel selectorStack = new StackPanel();
                    selectorStack.Children.Add(CreateSectionTitle("CHARACTER"));
                    ComboBox combo = CreateNamedSlotCombo(slots, false);
                    combo.Width = 245;
                    int selectedIndex = slots.FindIndex(slot => slot.Index == selectedCharacterSlotIndex);
                    if (selectedIndex >= 0) {
                        combo.SelectedIndex = selectedIndex;
                    }
                    combo.SelectionChanged += (_, _) => {
                        CharacterSlotInfo? selected = GetSelectedSlotInfo(combo);
                        if (selected != null) {
                            selectedCharacterSlotIndex = selected.Index;
                        }
                    };
                    selectorStack.Children.Add(combo);
                    selectorCard.Child = selectorStack;
                    root.Children.Add(selectorCard);
                }
            }
            ContentHost.Children.Add(root);
            StatusText.Text = "Character > " + title;
        }
        private CharacterProfileInfo ReadCharacterProfileInfo(string savePath, int slotIndex, bool includeRunes = true) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            string name = ReadFixedUtf16String(data, profileOffset, 32);
            uint level = BitConverter.ToUInt32(data, profileOffset + 0x22);
            uint secondsPlayed = BitConverter.ToUInt32(data, profileOffset + 0x26);
            uint runes = 0;
            if (includeRunes) {
                int playerDataOffset = FindPlayerGameDataOffset(data, slotIndex, level, name);
                runes = BitConverter.ToUInt32(data, playerDataOffset - 331);
            }
            byte bodyType = data[profileOffset + 0x242];
            byte archetype = data[profileOffset + 0x243];
            return new CharacterProfileInfo {
                SlotIndex = slotIndex, Name = string.IsNullOrWhiteSpace(name)? "Unknown" : name, Level = level, SecondsPlayed = secondsPlayed, Runes = runes, BodyType = bodyType, Archetype = archetype
            };
        }
        private static int FindPlayerGameDataOffset(byte[] data, int slotIndex, uint expectedLevel, string expectedName) {
            int slotStart = GetCharacterSlotDataOffset(slotIndex);
            int slotEnd = slotStart + EldenRingSlotDataSize;
            byte[] pattern = new byte[64];
            for (int block = 0; block<4; block++) {
                int offset = block * 16;
                pattern[offset] = 0x00;
                pattern[offset + 1] = 0xFF;
                pattern[offset + 2] = 0xFF;
                pattern[offset + 3] = 0xFF;
                pattern[offset + 4] = 0xFF;
            }
            int lastPossible = slotEnd - pattern.Length;
            for (int position = slotStart + 400; position <= lastPossible; position++) {
                if (data[position] != 0x00 || data[position + 1] != 0xFF || data[position + 2] != 0xFF || data[position + 3] != 0xFF || data[position + 4] != 0xFF) {
                    continue;
                }
                bool matches = true;
                for (int i = 0; i<pattern.Length; i++) {
                    if (data[position + i] != pattern[i]) {
                        matches = false;
                        break;
                    }
                }
                if (!matches) {
                    continue;
                }
                int levelOffset = position - 335;
                int nameOffset = position - 283;
                int runesOffset = position - 331;
                if (levelOffset<slotStart || runesOffset + 4> slotEnd || nameOffset<slotStart) {
                    continue;
                }
                uint candidateLevel = BitConverter.ToUInt32(data, levelOffset);
                if (candidateLevel != expectedLevel) {
                    continue;
                }
                string candidateName = ReadFixedUtf16String(data, nameOffset, 32);
                if (!string.IsNullOrWhiteSpace(expectedName) &&!candidateName.Equals(expectedName, StringComparison.Ordinal)) {
                    continue;
                }
                return position;
            }
            if (!string.IsNullOrWhiteSpace(expectedName)) {
                byte[] nameBytes = Encoding.Unicode.GetBytes(expectedName);
                int maxNameStart = slotEnd - Math.Max(nameBytes.Length + 2, 34);
                for (int nameOffset = slotStart; nameOffset <= maxNameStart; nameOffset++) {
                    bool nameMatch = true;
                    for (int i = 0; i<nameBytes.Length; i++) {
                        if (data[nameOffset + i] != nameBytes[i]) {
                            nameMatch = false;
                            break;
                        }
                    }
                    if (!nameMatch) continue;
                    int terminator = nameOffset + nameBytes.Length;
                    if (terminator + 1<slotEnd && (data[terminator] != 0 || data[terminator + 1] != 0)) {
                        continue;
                    }
                    int levelOffset = nameOffset - 52;
                    int candidateMagicOffset = levelOffset + 335;
                    if (levelOffset<slotStart || levelOffset + 4> slotEnd || candidateMagicOffset - 379<slotStart || candidateMagicOffset + 64> slotEnd) {
                        continue;
                    }
                    uint candidateLevel = BitConverter.ToUInt32(data, levelOffset);
                    if (candidateLevel != expectedLevel) continue;
                    bool plausibleStats = true;
                    int[] statOffsets = {
                        -379, -375, -371, -367, -363, -359, -355, -351
                    };
                    foreach (int relativeOffset in statOffsets) {
                        uint value = BitConverter.ToUInt32(data, candidateMagicOffset + relativeOffset);
                        if (value<1 || value> 99) {
                            plausibleStats = false;
                            break;
                        }
                    }
                    if (plausibleStats) return candidateMagicOffset;
                }
            }
            throw new InvalidDataException("Could not locate PlayerGameData for the selected character.");
        }
        private static string FormatCharacterPlayTime(uint seconds) {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            int totalHours = (int) time.TotalHours;
            return totalHours + "h " + time.Minutes + "m";
        }
        private static string GetBodyTypeName(byte bodyType) {
            return bodyType == 1? "Type A" : "Type B";
        }
        private static string GetArchetypeName(byte archetype) {
            return archetype switch {
                0 => "Vagabond", 1 => "Warrior", 2 => "Hero", 3 => "Bandit", 4 => "Astrologer", 5 => "Prophet", 6 => "Confessor", 7 => "Samurai", 8 => "Prisoner", 9 => "Wretch", _ => "Unknown (" + archetype + ")"
            };
        }
        private void BuildSaveBackupRestorePage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Backup & Restore", "Create manual save backups and restore previous versions of the loaded save."));
            Border retentionCard = CreateCard();
            retentionCard.Margin = new Thickness(0, 0, 0, 14);
            StackPanel retentionStack = new StackPanel();
            retentionStack.Children.Add(CreateSectionTitle("BACKUP RETENTION"));
            retentionStack.Children.Add(CreateFieldLabel("Toolkit keeps the 5 newest automatic backups and the 5 newest manual backups for each save. Character editing uses one safety backup at a time. Older backups are removed automatically."));
            retentionCard.Child = retentionStack;
            root.Children.Add(retentionCard);
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                Border lockedCard = CreateCard();
                StackPanel lockedStack = new StackPanel();
                lockedStack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
                lockedStack.Children.Add(CreateFieldLabel("Load a save first from Save > Save Import."));
                Button goToImportButton = new Button {
                    Content = "Go to Save Import", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0)
                };
                goToImportButton.Click += (_, _) => {
                    NavigateToSidebarTool("Save Import");
                };
                lockedStack.Children.Add(goToImportButton);
                lockedCard.Child = lockedStack;
                root.Children.Add(lockedCard);
                ContentHost.Children.Add(root);
                StatusText.Text = "Save > Backup & Restore";
                return;
            }
            Border currentSaveCard = CreateCard();
            currentSaveCard.Margin = new Thickness(0, 0, 0, 14);
            StackPanel currentSaveStack = new StackPanel();
            currentSaveStack.Children.Add(CreateSectionTitle("CURRENT SAVE"));
            FileInfo currentInfo = new FileInfo(loadedSavePath);
            string accountName = Directory.GetParent(loadedSavePath)?.Name ?? "Unknown";
            currentSaveStack.Children.Add(CreateInfoLine("Account", accountName));
            currentSaveStack.Children.Add(CreateInfoLine("Save", Path.GetFileName(loadedSavePath)));
            currentSaveStack.Children.Add(CreateInfoLine("Modified", currentInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
            StackPanel currentButtons = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            Button createBackupButton = new Button {
                Content = "Create Backup", Style = (Style) FindResource("ActionButton")
            };
            Button openBackupFolderButton = new Button {
                Content = "Open Backup Folder", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            createBackupButton.Click += (_, _) => {
                try {
                    CreateSaveBackup(loadedSavePath, "manual");
                    ShowToast("Save backup created", ToastKind.Success);
                    BuildSaveBackupRestorePage();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Create Backup", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            openBackupFolderButton.Click += (_, _) => {
                string folder = GetSaveBackupFolder(loadedSavePath);
                Directory.CreateDirectory(folder);
                OpenFolderInExplorer(folder);
            };
            currentButtons.Children.Add(createBackupButton);
            currentButtons.Children.Add(openBackupFolderButton);
            currentSaveStack.Children.Add(currentButtons);
            currentSaveCard.Child = currentSaveStack;
            root.Children.Add(currentSaveCard);
            Border restoreCard = CreateCard();
            StackPanel restoreStack = new StackPanel();
            restoreStack.Children.Add(CreateSectionTitle("RESTORE"));
            restoreStack.Children.Add(CreateFieldLabel("Choose a backup below. Restoring replaces the loaded save and creates one safety backup first."));
            List<SaveBackupInfo> backups = GetSaveBackups(loadedSavePath);
            if (backups.Count == 0) {
                TextBlock emptyText = new TextBlock {
                    Text = "No backups yet.", Foreground = GetResourceBrush("TextMuted"), Margin = new Thickness(0, 14, 0, 0)
                };
                restoreStack.Children.Add(emptyText);
            } else {
                ListBox backupList = new ListBox {
                    Background = GetResourceBrush("BgPanel"), BorderBrush = GetResourceBrush("BorderMain"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 14, 0, 0), MaxHeight = 270, HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                ScrollViewer.SetHorizontalScrollBarVisibility(backupList, ScrollBarVisibility.Disabled);
                Dictionary<ListBoxItem, Border> rowBorders = new Dictionary<ListBoxItem, Border>();
                foreach (SaveBackupInfo backup in backups) {
                    Border rowBorder = new Border {
                        Background = GetResourceBrush("BgPanel2"), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2)
                    };
                    Grid rowGrid = new Grid {
                        Margin = new Thickness(10, 7, 10, 7)
                    };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(160)
                    });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(120)
                    });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(1, GridUnitType.Star)
                    });
                    TextBlock timeText = new TextBlock {
                        Text = backup.Created.ToString("yyyy-MM-dd HH:mm:ss"), Foreground = GetResourceBrush("TextMain"), FontWeight = FontWeights.SemiBold
                    };
                    Grid.SetColumn(timeText, 0);
                    rowGrid.Children.Add(timeText);
                    TextBlock typeText = new TextBlock {
                        Text = backup.Operation.ToUpperInvariant(), Foreground = GetResourceBrush("AccentBright"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(typeText, 1);
                    rowGrid.Children.Add(typeText);
                    TextBlock fileText = new TextBlock {
                        Text = Path.GetFileName(backup.FullPath), Foreground = GetResourceBrush("TextMuted"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(fileText, 2);
                    rowGrid.Children.Add(fileText);
                    rowBorder.Child = rowGrid;
                    ListBoxItem item = new ListBoxItem {
                        Content = rowBorder, Tag = backup, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Margin = new Thickness(5, 5, 5, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0)
                    };
                    rowBorders[item] = rowBorder;
                    backupList.Items.Add(item);
                }
                TextBlock restoreNote = new TextBlock {
                    Text = "SELECT A BACKUP TO RESTORE.", Foreground = warningBrush, FontWeight = FontWeights.Bold, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0)
                };
                StackPanel restoreButtons = new StackPanel {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0)
                };
                Button restoreButton = new Button {
                    Content = "Restore Selected", Style = (Style) FindResource("ActionButton"), IsEnabled = false
                };
                Button restoreLatestButton = new Button {
                    Content = "Restore Latest", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
                };
                backupList.SelectionChanged += (_, _) => {
                    foreach (KeyValuePair<ListBoxItem, Border> pair in rowBorders) {
                        bool selected = pair.Key.IsSelected;
                        pair.Value.Background = selected? GetResourceBrush("AccentSelection") : GetResourceBrush("BgPanel2");
                        pair.Value.BorderBrush = selected? GetResourceBrush("AccentBright") : Brushes.Transparent;
                    }
                    if (backupList.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is SaveBackupInfo backup) {
                        restoreButton.IsEnabled = true;
                        restoreNote.Text = "WILL RESTORE " + backup.Created.ToString("yyyy-MM-dd HH:mm:ss") + " • CURRENT SAVE BACKED UP FIRST.";
                    } else {
                        restoreButton.IsEnabled = false;
                        restoreNote.Text = "SELECT A BACKUP TO RESTORE.";
                    }
                };
                restoreButton.Click += (_, _) => {
                    if (backupList.SelectedItem is not ListBoxItem selectedItem || selectedItem.Tag is not SaveBackupInfo backup) {
                        return;
                    }
                    RestoreSaveBackup(backup);
                };
                restoreLatestButton.Click += (_, _) => {
                    if (backups.Count == 0) {
                        return;
                    }
                    RestoreSaveBackup(backups[0]);
                };
                restoreButtons.Children.Add(restoreButton);
                restoreButtons.Children.Add(restoreLatestButton);
                restoreStack.Children.Add(backupList);
                restoreStack.Children.Add(restoreNote);
                restoreStack.Children.Add(restoreButtons);
            }
            restoreCard.Child = restoreStack;
            root.Children.Add(restoreCard);
            ContentHost.Children.Add(root);
            StatusText.Text = "Save > Backup & Restore";
        }
        private void RestoreSaveBackup(SaveBackupInfo backup) {
            try {
                if (!File.Exists(backup.FullPath)) {
                    throw new FileNotFoundException("The selected backup file no longer exists.");
                }
                CreateSaveBackup(loadedSavePath, "pre_restore");
                byte[] backupData = File.ReadAllBytes(backup.FullPath);
                ValidatePcSaveData(backupData);
                WriteSaveAtomically(loadedSavePath, backupData);
                ShowToast("Save restored successfully", ToastKind.Success);
                BuildSaveBackupRestorePage();
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Restore Save", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private List<SaveBackupInfo> GetSaveBackups(string savePath) {
            List<SaveBackupInfo> backups = new List<SaveBackupInfo>();
            string folder = GetSaveBackupFolder(savePath);
            if (Directory.Exists(folder)) {
                foreach (string file in Directory.GetFiles(folder)) {
                    string extension = Path.GetExtension(file);
                    if (!extension.Equals(".sl2", StringComparison.OrdinalIgnoreCase) &&!extension.Equals(".co2", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    SaveBackupInfo info = ParseSaveBackupInfo(file);
                    backups.Add(info);
                }
            }
            string legacyRoot = Path.Combine(settingsFolder, "Backups", "Saves");
            if (Directory.Exists(legacyRoot)) {
                string baseName = Path.GetFileNameWithoutExtension(savePath);
                string extension = Path.GetExtension(savePath);
                foreach (string file in Directory.GetFiles(legacyRoot, baseName + "_*" + extension, SearchOption.TopDirectoryOnly)) {
                    if (backups.Any(backup => backup.FullPath.Equals(file, StringComparison.OrdinalIgnoreCase))) {
                        continue;
                    }
                    backups.Add(ParseSaveBackupInfo(file));
                }
            }
            return backups.OrderByDescending(backup => backup.Created).ToList();
        }
        private SaveBackupInfo ParseSaveBackupInfo(string path) {
            string name = Path.GetFileNameWithoutExtension(path);
            string operation = "backup";
            DateTime created = File.GetLastWriteTime(path);
            string[] parts = name.Split('_');
            if (parts.Length >= 4) {
                string datePart = parts[parts.Length - 2];
                string timePart = parts[parts.Length - 1];
                string maybeOperation = parts[parts.Length - 3];
                if (DateTime.TryParseExact(datePart + "_" + timePart, "yyyyMMdd_HHmmssfff", null, System.Globalization.DateTimeStyles.None, out DateTime parsedCompact)) {
                    created = parsedCompact;
                    operation = maybeOperation;
                } else if (DateTime.TryParseExact(datePart + "_" + timePart, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out DateTime parsed)) {
                    created = parsed;
                    operation = maybeOperation;
                }
            }
            if (operation.Equals("backup", StringComparison.OrdinalIgnoreCase)) {
                string lower = name.ToLowerInvariant();
                foreach (string known in new[] {
                    "manual", "copy", "transfer", "import", "delete", "pre_restore"
                }) {
                    if (lower.Contains("_" + known + "_")) {
                        operation = known;
                        break;
                    }
                }
            }
            return new SaveBackupInfo {
                FullPath = path, Created = created, Operation = operation
            };
        }
        private string GetSaveBackupFolder(string savePath) {
            string account = Directory.GetParent(savePath)?.Name ?? "Unknown";
            string extensionName = Path.GetExtension(savePath).TrimStart('.').ToUpperInvariant();
            return Path.Combine(settingsFolder, "Backups", "Saves", account + "_" + extensionName);
        }
        private void BuildCharacterManagerPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Character Manager", "Copy, transfer, export, import, or delete character slots from the loaded save."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                Border lockedCard = CreateCard();
                StackPanel lockedStack = new StackPanel();
                lockedStack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
                lockedStack.Children.Add(CreateFieldLabel("Load a save first from Save > Save Import."));
                Button goToImportButton = new Button {
                    Content = "Go to Save Import", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0)
                };
                goToImportButton.Click += (_, _) => {
                    NavigateToSidebarTool("Save Import");
                };
                lockedStack.Children.Add(goToImportButton);
                lockedCard.Child = lockedStack;
                root.Children.Add(lockedCard);
                ContentHost.Children.Add(root);
                return;
            }
            List<CharacterSlotInfo> slots;
            try {
                slots = ReadCharacterSlots(loadedSavePath);
            } catch (Exception ex) {
                Border errorCard = CreateCard();
                StackPanel errorStack = new StackPanel();
                errorStack.Children.Add(CreateSectionTitle("SAVE READ ERROR"));
                errorStack.Children.Add(CreateFieldLabel(ex.Message));
                errorCard.Child = errorStack;
                root.Children.Add(errorCard);
                ContentHost.Children.Add(root);
                return;
            }
            root.Children.Add(BuildCopyCharacterCard(slots));
            root.Children.Add(BuildTransferCharacterCard(slots));
            root.Children.Add(BuildExportCharacterCard(slots));
            root.Children.Add(BuildImportCharacterCard(slots));
            root.Children.Add(BuildDeleteCharacterCard(slots));
            ContentHost.Children.Add(root);
            StatusText.Text = "Save > Character Manager";
        }
        private string?ShowTransferTargetSavePicker() {
            string?selectedPath = null;
            List<string> saves = FindDefaultEldenRingSaves().Where(path =>!path.Equals(loadedSavePath, StringComparison.OrdinalIgnoreCase)).ToList();
            Window pickerWindow = new Window {
                Title = "Select Target Save", Width = 760, Height = 430, MinWidth = 640, MinHeight = 340, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = GetResourceBrush("BgMain"), Foreground = GetResourceBrush("TextMain"), ResizeMode = ResizeMode.CanResize
            };
            Grid root = new Grid {
                Margin = new Thickness(16)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            Grid header = new Grid {
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            header.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            StackPanel headerText = new StackPanel();
            headerText.Children.Add(new TextBlock {
                Text = "Target Save", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = GetResourceBrush("TextMain")
            });
            headerText.Children.Add(new TextBlock {
                Text = "Choose an auto-detected save or browse manually.", Margin = new Thickness(0, 4, 0, 0), Foreground = GetResourceBrush("TextSoft")
            });
            Grid.SetColumn(headerText, 0);
            header.Children.Add(headerText);
            Button browseButton = new Button {
                Content = "Browse Save...", Style = (Style) FindResource("ActionButton"), VerticalAlignment = VerticalAlignment.Center
            };
            browseButton.Click += (_, _) => {
                OpenFileDialog dialog = new OpenFileDialog {
                    Title = "Select Target Elden Ring Save", Filter = "Elden Ring Saves (*.sl2;*.co2)|*.sl2;*.co2|Original Save (*.sl2)|*.sl2|Seamless Co-op Save (*.co2)|*.co2|All Files (*.*)|*.*", Multiselect = false
                };
                bool?result = dialog.ShowDialog();
                if (result != true) {
                    return;
                }
                if (dialog.FileName.Equals(loadedSavePath, StringComparison.OrdinalIgnoreCase)) {
                    MessageBox.Show("Please select a different target save.", "Target Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                selectedPath = dialog.FileName;
                pickerWindow.DialogResult = true;
            };
            Grid.SetColumn(browseButton, 1);
            header.Children.Add(browseButton);
            Grid.SetRow(header, 0);
            root.Children.Add(header);
            ListBox saveList = new ListBox {
                Background = GetResourceBrush("BgPanel"), BorderBrush = GetResourceBrush("BorderMain"), BorderThickness = new Thickness(1), Padding = new Thickness(5), HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(saveList, ScrollBarVisibility.Disabled);
            Dictionary<ListBoxItem, Border> rowBorders = new Dictionary<ListBoxItem, Border>();
            foreach (string savePath in saves) {
                string extension = Path.GetExtension(savePath);
                string type = extension.Equals(".co2", StringComparison.OrdinalIgnoreCase)? "Seamless" : "Original";
                string accountFolder = Directory.GetParent(savePath)?.Name ?? "Unknown";
                Border rowBorder = new Border {
                    Background = GetResourceBrush("BgPanel2"), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2)
                };
                Grid row = new Grid {
                    Margin = new Thickness(9, 7, 9, 7)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(90)
                });
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(150)
                });
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                TextBlock typeText = new TextBlock {
                    Text = type, Foreground = GetResourceBrush("AccentBright"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(typeText, 0);
                row.Children.Add(typeText);
                TextBlock accountText = new TextBlock {
                    Text = accountFolder, Foreground = GetResourceBrush("TextMain"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(accountText, 1);
                row.Children.Add(accountText);
                TextBlock pathText = new TextBlock {
                    Text = savePath, Foreground = GetResourceBrush("TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(pathText, 2);
                row.Children.Add(pathText);
                rowBorder.Child = row;
                ListBoxItem item = new ListBoxItem {
                    Content = rowBorder, Tag = savePath, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 4), Background = Brushes.Transparent, BorderThickness = new Thickness(0)
                };
                rowBorders[item] = rowBorder;
                saveList.Items.Add(item);
            }
            Grid.SetRow(saveList, 1);
            root.Children.Add(saveList);
            StackPanel footer = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0)
            };
            Button cancelButton = new Button {
                Content = "Cancel", Style = (Style) FindResource("ActionButton"), MinWidth = 96
            };
            Button selectButton = new Button {
                Content = "Select", Style = (Style) FindResource("ActionButton"), MinWidth = 104, Margin = new Thickness(8, 0, 0, 0), Opacity = 0.55, IsHitTestVisible = false
            };
            saveList.SelectionChanged += (_, _) => {
                foreach (KeyValuePair<ListBoxItem, Border> pair in rowBorders) {
                    bool selected = pair.Key.IsSelected;
                    pair.Value.Background = selected? GetResourceBrush("AccentSelection") : GetResourceBrush("BgPanel2");
                    pair.Value.BorderBrush = selected? GetResourceBrush("AccentBright") : Brushes.Transparent;
                }
                bool hasSelection = saveList.SelectedItem is ListBoxItem;
                selectButton.IsHitTestVisible = hasSelection;
                selectButton.Opacity = hasSelection? 1.0 : 0.55;
                selectButton.Background = hasSelection? GetResourceBrush("AccentButton") : GetResourceBrush("BgPanel2");
                selectButton.BorderBrush = hasSelection? GetResourceBrush("Accent") : GetResourceBrush("BorderSoft");
            };
            selectButton.Click += (_, _) => {
                if (saveList.SelectedItem is not ListBoxItem selectedItem || selectedItem.Tag is not string path) {
                    return;
                }
                selectedPath = path;
                pickerWindow.DialogResult = true;
            };
            saveList.MouseDoubleClick += (_, _) => {
                if (saveList.SelectedItem is not ListBoxItem selectedItem || selectedItem.Tag is not string path) {
                    return;
                }
                selectedPath = path;
                pickerWindow.DialogResult = true;
            };
            cancelButton.Click += (_, _) => {
                pickerWindow.DialogResult = false;
            };
            footer.Children.Add(cancelButton);
            footer.Children.Add(selectButton);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            pickerWindow.Content = root;
            pickerWindow.ShowDialog();
            return selectedPath;
        }
        private Border BuildCopyCharacterCard(List<CharacterSlotInfo> slots) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("COPY CHARACTER"));
            stack.Children.Add(CreateFieldLabel("Copy one character to another slot in this save."));
            StackPanel row = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            ComboBox sourceCombo = CreateNamedSlotCombo(slots, false);
            ComboBox targetCombo = CreateNamedSlotCombo(slots, true);
            targetCombo.Margin = new Thickness(8, 0, 0, 0);
            Button copyButton = new Button {
                Content = "Copy", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            TextBlock note = CreateOperationNote();
            void RefreshNote() {
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (target == null) {
                    note.Text = "Choose a target slot.";
                    return;
                }
                note.Text = target.IsEmpty? "Empty target • backup created before copy." : "Will overwrite " + target.Name + " • backup created first.";
            }
            targetCombo.SelectionChanged += (_, _) => {
                RefreshNote();
            };
            copyButton.Click += (_, _) => {
                CharacterSlotInfo? source = GetSelectedSlotInfo(sourceCombo);
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (source == null || source.IsEmpty || target == null) {
                    MessageBox.Show("Select a character source and a target slot.", "Copy Character", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (source.Index == target.Index) {
                    MessageBox.Show("Source and target slots must be different.", "Copy Character", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try {
                    CreateSaveBackup(loadedSavePath, "copy");
                    CopyCharacterSlot(loadedSavePath, source.Index, loadedSavePath, target.Index, false);
                    ShowToast("Character copied successfully", ToastKind.Success);
                    BuildCharacterManagerPage();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Copy Character", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            row.Children.Add(sourceCombo);
            row.Children.Add(targetCombo);
            row.Children.Add(copyButton);
            stack.Children.Add(row);
            stack.Children.Add(note);
            card.Child = stack;
            RefreshNote();
            return card;
        }
        private Border BuildTransferCharacterCard(List<CharacterSlotInfo> sourceSlots) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("TRANSFER TO ANOTHER SAVE"));
            stack.Children.Add(CreateFieldLabel("Copy a character into another Elden Ring save."));
            ComboBox sourceCombo = CreateNamedSlotCombo(sourceSlots, false);
            ComboBox targetCombo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 245, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = false
            };
            Button selectSaveButton = new Button {
                Content = "Select Target Save...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            Button transferButton = new Button {
                Content = "Transfer", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0), IsEnabled = false
            };
            TextBlock saveText = new TextBlock {
                Text = "No target save selected", Foreground = GetResourceBrush("TextMuted"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 300
            };
            TextBlock note = CreateOperationNote();
            List<CharacterSlotInfo> targetSlots = new List<CharacterSlotInfo>();
            void RefreshNote() {
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (target == null) {
                    note.Text = "Select a target save and slot.";
                    return;
                }
                note.Text = target.IsEmpty? "Empty target • target save backed up before transfer." : "Will overwrite " + target.Name + " • target save backed up first.";
            }
            selectSaveButton.Click += (_, _) => {
                string?selected = ShowTransferTargetSavePicker();
                if (string.IsNullOrWhiteSpace(selected)) {
                    return;
                }
                try {
                    targetSlots = ReadCharacterSlots(selected);
                    transferTargetSavePath = selected;
                    PopulateSlotCombo(targetCombo, targetSlots, true);
                    targetCombo.IsEnabled = true;
                    transferButton.IsEnabled = true;
                    string targetFolderName = Directory.GetParent(selected)?.Name ?? "";
                    saveText.Text = (string.IsNullOrWhiteSpace(targetFolderName)? Path.GetFileName(selected) : targetFolderName + "/" + Path.GetFileName(selected));
                    saveText.ToolTip = selected;
                    RefreshNote();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Target Save", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            targetCombo.SelectionChanged += (_, _) => {
                RefreshNote();
            };
            transferButton.Click += (_, _) => {
                CharacterSlotInfo? source = GetSelectedSlotInfo(sourceCombo);
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (source == null || source.IsEmpty || target == null || string.IsNullOrWhiteSpace(transferTargetSavePath)) {
                    MessageBox.Show("Select a source character, target save, and target slot.", "Transfer Character", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try {
                    CreateSaveBackup(transferTargetSavePath, "transfer");
                    CopyCharacterSlot(loadedSavePath, source.Index, transferTargetSavePath, target.Index, true);
                    ShowToast("Character transferred successfully", ToastKind.Success);
                    targetSlots = ReadCharacterSlots(transferTargetSavePath);
                    PopulateSlotCombo(targetCombo, targetSlots, true);
                    RefreshNote();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Transfer Character", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            StackPanel sourceRow = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            sourceRow.Children.Add(sourceCombo);
            sourceRow.Children.Add(selectSaveButton);
            sourceRow.Children.Add(saveText);
            stack.Children.Add(sourceRow);
            StackPanel targetRow = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0)
            };
            targetRow.Children.Add(targetCombo);
            targetRow.Children.Add(transferButton);
            stack.Children.Add(targetRow);
            stack.Children.Add(note);
            card.Child = stack;
            RefreshNote();
            return card;
        }
        private Border BuildExportCharacterCard(List<CharacterSlotInfo> slots) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("EXPORT CHARACTER"));
            stack.Children.Add(CreateFieldLabel("Export one character as a standalone Toolkit file."));
            StackPanel row = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            ComboBox sourceCombo = CreateNamedSlotCombo(slots, false);
            Button exportButton = new Button {
                Content = "Export...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            exportButton.Click += (_, _) => {
                CharacterSlotInfo? source = GetSelectedSlotInfo(sourceCombo);
                if (source == null || source.IsEmpty) {
                    ShowToast("Select a character to export", ToastKind.Info);
                    return;
                }
                SaveFileDialog dialog = new SaveFileDialog {
                    Title = "Export Character", Filter = "Elden Ring Toolkit Character (*.erch)|*.erch", DefaultExt = ".erch", AddExtension = true, FileName = SanitizeFileName(source.Name) + ".erch"
                };
                bool?result = dialog.ShowDialog();
                if (result != true) {
                    return;
                }
                try {
                    ExportCharacterFile(loadedSavePath, source.Index, dialog.FileName);
                    ShowToast("Character exported successfully", ToastKind.Success);
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Export Character", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            row.Children.Add(sourceCombo);
            row.Children.Add(exportButton);
            stack.Children.Add(row);
            TextBlock note = CreateOperationNote();
            note.Text = "Export does not modify the loaded save.";
            stack.Children.Add(note);
            card.Child = stack;
            return card;
        }
        private Border BuildImportCharacterCard(List<CharacterSlotInfo> slots) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 14);
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("IMPORT CHARACTER"));
            stack.Children.Add(CreateFieldLabel("Import a Toolkit character file into a save slot."));
            StackPanel row = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            Button importButton = new Button {
                Content = "Import...", Style = (Style) FindResource("ActionButton")
            };
            ComboBox targetCombo = CreateNamedSlotCombo(slots, true);
            targetCombo.Margin = new Thickness(8, 0, 0, 0);
            TextBlock note = CreateOperationNote();
            void RefreshNote() {
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (target == null) {
                    note.Text = "Choose a target slot.";
                    return;
                }
                note.Text = target.IsEmpty? "Empty target • backup created before import." : "Will overwrite " + target.Name + " • backup created first.";
            }
            targetCombo.SelectionChanged += (_, _) => {
                RefreshNote();
            };
            importButton.Click += (_, _) => {
                CharacterSlotInfo? target = GetSelectedSlotInfo(targetCombo);
                if (target == null) {
                    return;
                }
                OpenFileDialog dialog = new OpenFileDialog {
                    Title = "Import Character", Filter = "Elden Ring Toolkit Character (*.erch;*.ertchar)|*.erch;*.ertchar|Current Format (*.erch)|*.erch|Legacy Format (*.ertchar)|*.ertchar", Multiselect = false
                };
                bool?result = dialog.ShowDialog();
                if (result != true) {
                    return;
                }
                try {
                    CreateSaveBackup(loadedSavePath, "import");
                    ImportCharacterFile(loadedSavePath, target.Index, dialog.FileName);
                    ShowToast("Character imported successfully", ToastKind.Success);
                    BuildCharacterManagerPage();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Import Character", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            row.Children.Add(importButton);
            row.Children.Add(targetCombo);
            stack.Children.Add(row);
            stack.Children.Add(note);
            card.Child = stack;
            RefreshNote();
            return card;
        }
        private Border BuildDeleteCharacterCard(List<CharacterSlotInfo> slots) {
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("DELETE CHARACTER"));
            stack.Children.Add(CreateFieldLabel("Clear a character slot from the loaded save."));
            StackPanel row = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            ComboBox sourceCombo = CreateNamedSlotCombo(slots, false);
            Button deleteButton = new Button {
                Content = "Delete...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            TextBlock note = CreateOperationNote();
            note.FontSize = 12.5;
            note.FontWeight = FontWeights.Bold;
            void RefreshNote() {
                CharacterSlotInfo? source = GetSelectedSlotInfo(sourceCombo);
                note.Text = source == null? "SELECT A CHARACTER SLOT." : "WILL DELETE " + source.Name.ToUpperInvariant() + " • BACKUP CREATED FIRST.";
            }
            sourceCombo.SelectionChanged += (_, _) => {
                RefreshNote();
            };
            deleteButton.Click += (_, _) => {
                CharacterSlotInfo? source = GetSelectedSlotInfo(sourceCombo);
                if (source == null || source.IsEmpty) {
                    return;
                }
                try {
                    CreateSaveBackup(loadedSavePath, "delete");
                    DeleteCharacterSlot(loadedSavePath, source.Index);
                    ShowToast("Character deleted successfully", ToastKind.Success);
                    BuildCharacterManagerPage();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Delete Character", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            row.Children.Add(sourceCombo);
            row.Children.Add(deleteButton);
            stack.Children.Add(row);
            note.Foreground = warningBrush;
            stack.Children.Add(note);
            card.Child = stack;
            RefreshNote();
            return card;
        }
        private TextBlock CreateOperationNote() {
            return new TextBlock {
                Foreground = GetResourceBrush("TextMuted"), FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
            };
        }
        private ComboBox CreateNamedSlotCombo(List<CharacterSlotInfo> slots, bool includeEmpty) {
            ComboBox combo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 245, HorizontalAlignment = HorizontalAlignment.Left
            };
            PopulateSlotCombo(combo, slots, includeEmpty);
            return combo;
        }
        private void PopulateSlotCombo(ComboBox combo, List<CharacterSlotInfo> slots, bool includeEmpty) {
            combo.Items.Clear();
            foreach (CharacterSlotInfo slot in slots) {
                if (!includeEmpty && slot.IsEmpty) {
                    continue;
                }
                string displayText = slot.IsEmpty?(slot.Index + 1) + " - Empty" : (slot.Index + 1) + " - " + slot.Name + " · Lv. " + slot.Level;
                ComboBoxItem item = new ComboBoxItem {
                    Content = displayText, Tag = slot
                };
                combo.Items.Add(item);
            }
            if (combo.Items.Count> 0) {
                combo.SelectedIndex = 0;
            }
        }
        private CharacterSlotInfo? GetSelectedSlotInfo(ComboBox combo) {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is CharacterSlotInfo slot) {
                return slot;
            }
            return null;
        }
        private List<CharacterSlotInfo> ReadCharacterSlots(string savePath) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int activeOffset = EldenRingUserData10DataOffset + EldenRingActiveSlotsRelativeOffset;
            int profilesBase = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset;
            List<CharacterSlotInfo> result = new List<CharacterSlotInfo>();
            for (int index = 0; index<10; index++) {
                int slotDataOffset = GetCharacterSlotDataOffset(index);
                uint version = BitConverter.ToUInt32(data, slotDataOffset);
                bool active = data[activeOffset + index] != 0;
                bool empty = version == 0;
                int profileOffset = profilesBase + index * EldenRingProfileSize;
                string profileName = ReadFixedUtf16String(data, profileOffset, 32);
                int level = 0;
                if (!empty) {
                    level = BitConverter.ToInt32(data, profileOffset + 0x22);
                }
                string name;
                if (empty) {
                    name = "Empty";
                } else if (!string.IsNullOrWhiteSpace(profileName)) {
                    name = profileName;
                } else {
                    name = "Character " + (index + 1);
                }
                result.Add(new CharacterSlotInfo {
                    Index = index, Name = name, Level = level, IsEmpty = empty, IsActive = active
                });
            }
            return result;
        }
        private static string ReadFixedUtf16String(byte[] data, int offset, int byteCount) {
            string value = Encoding.Unicode.GetString(data, offset, byteCount);
            int nullIndex = value.IndexOf('\0');
            if (nullIndex >= 0) {
                value = value.Substring(0, nullIndex);
            }
            return value.Trim();
        }
        private static void ValidatePcSaveData(byte[] data) {
            if (data.Length<EldenRingMinimumPcSaveSize) {
                throw new InvalidDataException("The selected save file is too small or unsupported.");
            }
            if (data[0] != (byte) 'B' || data[1] != (byte) 'N' || data[2] != (byte) 'D' || data[3] != (byte) '4') {
                throw new InvalidDataException("This build currently supports standard PC BND4 .sl2/.co2 saves only.");
            }
        }
        private void CopyCharacterSlot(string sourceSavePath, int sourceSlot, string targetSavePath, int targetSlot, bool patchSteamId) {
            byte[] sourceData = File.ReadAllBytes(sourceSavePath);
            byte[] targetData = File.ReadAllBytes(targetSavePath);
            ValidatePcSaveData(sourceData);
            ValidatePcSaveData(targetData);
            List<CharacterSlotInfo> sourceSlots = ReadCharacterSlots(sourceSavePath);
            if (sourceSlot<0 || sourceSlot >= 10 || sourceSlots[sourceSlot].IsEmpty) {
                throw new InvalidOperationException("The selected source slot is empty.");
            }
            int sourceDataOffset = GetCharacterSlotDataOffset(sourceSlot);
            int targetDataOffset = GetCharacterSlotDataOffset(targetSlot);
            Buffer.BlockCopy(sourceData, sourceDataOffset, targetData, targetDataOffset, EldenRingSlotDataSize);
            int sourceProfileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + sourceSlot * EldenRingProfileSize;
            int targetProfileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + targetSlot * EldenRingProfileSize;
            Buffer.BlockCopy(sourceData, sourceProfileOffset, targetData, targetProfileOffset, EldenRingProfileSize);
            int targetActiveOffset = EldenRingUserData10DataOffset + EldenRingActiveSlotsRelativeOffset + targetSlot;
            targetData[targetActiveOffset] = 1;
            if (patchSteamId) {
                ulong sourceSteamId = ReadSaveSteamId(sourceData);
                ulong targetSteamId = ReadSaveSteamId(targetData);
                PatchCharacterSteamId(targetData, targetDataOffset, sourceSteamId, targetSteamId);
            }
            RecalculateSlotChecksum(targetData, targetSlot);
            RecalculateUserData10Checksum(targetData);
            WriteSaveAtomically(targetSavePath, targetData);
        }
        private void DeleteCharacterSlot(string savePath, int slotIndex) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            int dataOffset = GetCharacterSlotDataOffset(slotIndex);
            Array.Clear(data, dataOffset, EldenRingSlotDataSize);
            int checksumOffset = GetCharacterSlotChecksumOffset(slotIndex);
            Array.Clear(data, checksumOffset, 16);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            Array.Clear(data, profileOffset, EldenRingProfileSize);
            int activeOffset = EldenRingUserData10DataOffset + EldenRingActiveSlotsRelativeOffset + slotIndex;
            data[activeOffset] = 0;
            RecalculateUserData10Checksum(data);
            WriteSaveAtomically(savePath, data);
        }
        private void ExportCharacterFile(string savePath, int slotIndex, string outputPath) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            List<CharacterSlotInfo> slots = ReadCharacterSlots(savePath);
            if (slots[slotIndex].IsEmpty) {
                throw new InvalidOperationException("The selected slot is empty.");
            }
            int slotDataOffset = GetCharacterSlotDataOffset(slotIndex);
            byte[] slotData = new byte[EldenRingSlotDataSize];
            Buffer.BlockCopy(data, slotDataOffset, slotData, 0, slotData.Length);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            byte[] profile = new byte[EldenRingProfileSize];
            Buffer.BlockCopy(data, profileOffset, profile, 0, profile.Length);
            using MemoryStream payloadStream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(payloadStream, Encoding.UTF8, true)) {
                writer.Write(new byte[] {
                    (byte) 'E', (byte) 'R', (byte) 'T', (byte) 'C'
                });
                writer.Write(1);
                writer.Write(ReadSaveSteamId(data));
                writer.Write(slotData.Length);
                writer.Write(slotData);
                writer.Write(profile.Length);
                writer.Write(profile);
            }
            byte[] payload = payloadStream.ToArray();
            byte[] checksum = MD5.HashData(payload);
            using FileStream output = File.Create(outputPath);
            output.Write(payload, 0, payload.Length);
            output.Write(checksum, 0, checksum.Length);
        }
        private void ImportCharacterFile(string savePath, int targetSlot, string characterFilePath) {
            byte[] fileData = File.ReadAllBytes(characterFilePath);
            if (fileData.Length<32) {
                throw new InvalidDataException("Invalid .ertchar file.");
            }
            byte[] payload = fileData.Take(fileData.Length - 16).ToArray();
            byte[] storedChecksum = fileData.Skip(fileData.Length - 16).ToArray();
            byte[] calculatedChecksum = MD5.HashData(payload);
            if (!storedChecksum.SequenceEqual(calculatedChecksum)) {
                throw new InvalidDataException("The .ertchar file failed its integrity check.");
            }
            byte[] slotData;
            byte[] profile;
            ulong sourceSteamId;
            using MemoryStream stream = new MemoryStream(payload);
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            byte[] magic = reader.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != (byte) 'E' || magic[1] != (byte) 'R' || magic[2] != (byte) 'T' || magic[3] != (byte) 'C') {
                throw new InvalidDataException("Unsupported character file.");
            }
            int version = reader.ReadInt32();
            if (version != 1) {
                throw new InvalidDataException("Unsupported .ertchar version.");
            }
            sourceSteamId = reader.ReadUInt64();
            int slotSize = reader.ReadInt32();
            if (slotSize != EldenRingSlotDataSize) {
                throw new InvalidDataException("Unexpected character slot size.");
            }
            slotData = reader.ReadBytes(slotSize);
            int profileSize = reader.ReadInt32();
            if (profileSize != EldenRingProfileSize) {
                throw new InvalidDataException("Unexpected profile size.");
            }
            profile = reader.ReadBytes(profileSize);
            byte[] saveData = File.ReadAllBytes(savePath);
            ValidatePcSaveData(saveData);
            int targetDataOffset = GetCharacterSlotDataOffset(targetSlot);
            Buffer.BlockCopy(slotData, 0, saveData, targetDataOffset, slotData.Length);
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + targetSlot * EldenRingProfileSize;
            Buffer.BlockCopy(profile, 0, saveData, profileOffset, profile.Length);
            int activeOffset = EldenRingUserData10DataOffset + EldenRingActiveSlotsRelativeOffset + targetSlot;
            saveData[activeOffset] = 1;
            ulong targetSteamId = ReadSaveSteamId(saveData);
            PatchCharacterSteamId(saveData, targetDataOffset, sourceSteamId, targetSteamId);
            RecalculateSlotChecksum(saveData, targetSlot);
            RecalculateUserData10Checksum(saveData);
            WriteSaveAtomically(savePath, saveData);
        }
        private static void PatchCharacterSteamId(byte[] saveData, int slotDataOffset, ulong sourceSteamId, ulong targetSteamId) {
            if (sourceSteamId == 0 || targetSteamId == 0 || sourceSteamId == targetSteamId) {
                return;
            }
            byte[] sourceBytes = BitConverter.GetBytes(sourceSteamId);
            byte[] targetBytes = BitConverter.GetBytes(targetSteamId);
            List<int> matches = new List<int>();
            int end = slotDataOffset + EldenRingSlotDataSize - sourceBytes.Length;
            for (int offset = slotDataOffset; offset <= end; offset++) {
                bool match = true;
                for (int i = 0; i<sourceBytes.Length; i++) {
                    if (saveData[offset + i] != sourceBytes[i]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    matches.Add(offset);
                }
            }
            if (matches.Count != 1) {
                throw new InvalidDataException("SteamID patch could not be applied safely. Expected one character SteamID match, found " + matches.Count + ".");
            }
            Buffer.BlockCopy(targetBytes, 0, saveData, matches[0], targetBytes.Length);
        }
        private static ulong ReadSaveSteamId(byte[] data) {
            return BitConverter.ToUInt64(data, EldenRingUserData10DataOffset + 4);
        }
        private static int GetCharacterSlotChecksumOffset(int slotIndex) {
            return EldenRingFirstSlotChecksumOffset + slotIndex * EldenRingSlotStride;
        }
        private static int GetCharacterSlotDataOffset(int slotIndex) {
            return GetCharacterSlotChecksumOffset(slotIndex) + 16;
        }
        private static void RecalculateSlotChecksum(byte[] data, int slotIndex) {
            int checksumOffset = GetCharacterSlotChecksumOffset(slotIndex);
            int dataOffset = checksumOffset + 16;
            byte[] checksum = MD5.HashData(data.AsSpan(dataOffset, EldenRingSlotDataSize));
            Buffer.BlockCopy(checksum, 0, data, checksumOffset, checksum.Length);
        }
        private static void RecalculateUserData10Checksum(byte[] data) {
            byte[] checksum = MD5.HashData(data.AsSpan(EldenRingUserData10DataOffset, EldenRingUserData10DataSize));
            Buffer.BlockCopy(checksum, 0, data, EldenRingUserData10ChecksumOffset, checksum.Length);
        }
        private string CreateEditorSafetyBackupOnce() {
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                return "";
            }
            if (!string.IsNullOrWhiteSpace(editorSafetyBackupPath) && File.Exists(editorSafetyBackupPath)) {
                return editorSafetyBackupPath;
            }
            string backupFolder = GetSaveBackupFolder(loadedSavePath);
            if (Directory.Exists(backupFolder)) {
                foreach (string file in Directory.GetFiles(backupFolder)) {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Contains("_editor_session_", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            File.Delete(file);
                        } catch {
                        }
                    }
                }
            }
            editorSafetyBackupPath = CreateSaveBackup(loadedSavePath, "editor_session");
            return editorSafetyBackupPath;
        }
        private string CreateSaveBackup(string savePath, string operation) {
            string backupRoot = GetSaveBackupFolder(savePath);
            Directory.CreateDirectory(backupRoot);
            string extension = Path.GetExtension(savePath);
            string fileName = Path.GetFileNameWithoutExtension(savePath);
            string safeOperation = string.IsNullOrWhiteSpace(operation)? "backup" : operation.Trim().Replace(" ", "_");
            string backupPath = Path.Combine(backupRoot, fileName + "_" + safeOperation + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + extension);
            File.Copy(savePath, backupPath, false);
            PruneSaveBackups(savePath);
            return backupPath;
        }
        private void PruneSaveBackups(string savePath) {
            List<SaveBackupInfo> backups = GetSaveBackups(savePath);
            List<SaveBackupInfo> manualBackups = backups.Where(backup => backup.Operation.Equals("manual", StringComparison.OrdinalIgnoreCase)).OrderByDescending(backup => backup.Created).ToList();
            List<SaveBackupInfo> automaticBackups = backups.Where(backup =>!backup.Operation.Equals("manual", StringComparison.OrdinalIgnoreCase)).OrderByDescending(backup => backup.Created).ToList();
            DeleteOldBackups(manualBackups, 5);
            DeleteOldBackups(automaticBackups, 5);
        }
        private static void DeleteOldBackups(List<SaveBackupInfo> backups, int keepCount) {
            foreach (SaveBackupInfo backup in backups.Skip(keepCount)) {
                try {
                    if (File.Exists(backup.FullPath)) {
                        File.Delete(backup.FullPath);
                    }
                } catch {
                }
            }
        }
        private static void WriteSaveAtomically(string path, byte[] data) {
            string tempPath = path + ".erttmp";
            File.WriteAllBytes(tempPath, data);
            File.Copy(tempPath, path, true);
            File.Delete(tempPath);
        }
        private static string SanitizeFileName(string value) {
            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                value = value.Replace(invalid, '_');
            }
            if (string.IsNullOrWhiteSpace(value)) {
                return "Character";
            }
            return value;
        }
        private void BuildAboutOverviewPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("About", "A lightweight Elden Ring modding toolkit focused on clear workflows, safe replacements, and beginner-friendly tools."));
            Border brandCard = CreateCard();
            brandCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel brandStack = new StackPanel();
            TextBlock logo = new TextBlock {
                FontFamily = new FontFamily("Georgia"), FontSize = 30, FontWeight = FontWeights.SemiBold
            };
            logo.Inlines.Add(new System.Windows.Documents.Run("Elden Ring") {
                Foreground = GetResourceBrush("AccentBright"), FontWeight = FontWeights.Bold
            });
            logo.Inlines.Add(new System.Windows.Documents.Run(" Toolkit") {
                Foreground = normalTextBrush
            });
            brandStack.Children.Add(logo);
            TextBlock byline = new TextBlock {
                Text = "by EldenKane", Foreground = GetResourceBrush("Accent"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 3, 0, 14)
            };
            brandStack.Children.Add(byline);
            brandStack.Children.Add(CreateFieldLabel("Built as a practical desktop companion for Elden Ring modding: inspect game part IDs, remap skin replacements, detect Mod Engine conflicts, and manage installed replacements without manually juggling filenames."));
            brandCard.Child = brandStack;
            root.Children.Add(brandCard);
            Border featuresCard = CreateCard();
            StackPanel featureStack = new StackPanel();
            featureStack.Children.Add(CreateSectionTitle("CURRENT FEATURES"));
            string[] features = {
                "Item Replace with automatic type and weapon-class compatibility checks", "Conflict detection against the active Mod Engine parts folder", "Automatic remapping to selected or recommended game slots", "Installed Mods manager with backup, restore, remove, and file reveal", "Full parts database for Base Game, Shadow of the Erdtree, and character customization data currently used by the toolkit", "Armor, weapon subtypes, hair, accessories, face parts, and related replacement categories"
            };
            foreach (string feature in features) {
                featureStack.Children.Add(new TextBlock {
                    Text = "• " + feature, Foreground = normalTextBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0)
                });
            }
            featuresCard.Child = featureStack;
            root.Children.Add(featuresCard);
            ContentHost.Children.Add(root);
        }
        private void BuildAboutVersionPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Version", "Build and database information for this copy of Elden Ring Toolkit."));
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("TOOLKIT"));
            stack.Children.Add(CreateInfoLine("Product", "Elden Ring Toolkit"));
            stack.Children.Add(CreateInfoLine("Author", "EldenKane"));
            stack.Children.Add(CreateInfoLine("Version", "1.0b"));
            stack.Children.Add(CreateInfoLine("Framework", ".NET 10 / WPF"));
            stack.Children.Add(CreateInfoLine("Database Entries", itemDatabase.Count.ToString()));
            stack.Children.Add(CreateInfoLine("Database Status", EldenRingDatabase.LoadStatus));
            card.Child = stack;
            root.Children.Add(card);
            ContentHost.Children.Add(root);
        }
        private void BuildAboutCreditsPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Credits", "People, projects, and game data sources that make the toolkit possible."));
            Border card = CreateCard();
            StackPanel stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("PROJECT"));
            stack.Children.Add(CreateInfoLine("Design & Development", "EldenKane"));
            stack.Children.Add(CreateFieldLabel("Item ID and parts naming data is based on community Elden Ring modding references, including Nexus Mods Article 158."));
            TextBlock disclaimer = new TextBlock {
                Text = "Elden Ring and its game assets are property of FromSoftware and their respective publishers. Elden Ring Toolkit is an independent fan-made modding utility and is not affiliated with or endorsed by FromSoftware or Bandai Namco Entertainment.", Foreground = mutedTextBrush, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0)
            };
            stack.Children.Add(disclaimer);
            card.Child = stack;
            root.Children.Add(card);
            ContentHost.Children.Add(root);
        }
        private void DiscordServerSidebarButton_Click(object sender, RoutedEventArgs e) {
            OpenExternalLink(discordServerUrl, "Discord server link is not configured yet.");
        }
        private void DonateSidebarButton_Click(object sender, RoutedEventArgs e) {
            OpenExternalLink(donateUrl, "Donate link is not configured yet.");
        }
        private void OpenExternalLink(string url, string emptyMessage) {
            if (string.IsNullOrWhiteSpace(url)) {
                ShowToast(emptyMessage, ToastKind.Info);
                return;
            }
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = url, UseShellExecute = true
                });
            } catch {
                ShowToast("Could not open the link.", ToastKind.Warning);
            }
        }
        private void BuildSettingsPathsPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("General", "Configure list behavior and the data folders Elden Ring Toolkit uses."));
            Border behaviorCard = CreateCard();
            behaviorCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel behaviorStack = new StackPanel();
            behaviorStack.Children.Add(CreateSectionTitle("BEHAVIOR"));
            behaviorStack.Children.Add(CreateFieldLabel("Installed Mods entries rendered per page"));
            ComboBox pageSizeCombo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0)
            };
            foreach (int value in new[] {
                15, 25, 50
            }) {
                pageSizeCombo.Items.Add(value.ToString());
            }
            pageSizeCombo.SelectedItem = installedModsPageSize.ToString();
            pageSizeCombo.SelectionChanged += (_, _) => {
                if (pageSizeCombo.SelectedItem is string selected && int.TryParse(selected, out int value)) {
                    installedModsPageSize = value;
                    SaveToolkitSettings();
                }
            };
            behaviorStack.Children.Add(pageSizeCombo);
            behaviorCard.Child = behaviorStack;
            root.Children.Add(behaviorCard);
            Border gameDataCard = CreateCard();
            gameDataCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel gameDataStack = new StackPanel();
            gameDataStack.Children.Add(CreateSectionTitle("GAME DATA DIRECTORY"));
            gameDataStack.Children.Add(CreateFieldLabel("Select the Elden Ring folder containing eldenring.exe. Used for game data and high-resolution item icons."));
            gameDataStack.Children.Add(CreateInfoLine("Game Folder", string.IsNullOrWhiteSpace(gameDataFolder)? "Not configured" : gameDataFolder));
            StackPanel gameButtons = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0)
            };
            Button setGameFolderButton = new Button {
                Content = "Select Game Directory...", Style = (Style) FindResource("ActionButton")
            };
            setGameFolderButton.Click += (_, _) => {
                ChooseGameDataFolder();
                BuildSettingsPathsPage();
            };
            Button openGameFolderButton = new Button {
                Content = "Open Folder", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0), IsEnabled =!string.IsNullOrWhiteSpace(gameDataFolder) && Directory.Exists(gameDataFolder)
            };
            openGameFolderButton.Click += (_, _) => {
                OpenFolderInExplorer(gameDataFolder);
            };
            gameButtons.Children.Add(setGameFolderButton);
            gameButtons.Children.Add(openGameFolderButton);
            gameDataStack.Children.Add(gameButtons);
            gameDataCard.Child = gameDataStack;
            root.Children.Add(gameDataCard);
            Border modWorkspaceCard = CreateCard();
            modWorkspaceCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel modWorkspaceStack = new StackPanel();
            modWorkspaceStack.Children.Add(CreateSectionTitle("MOD ENGINE 2 DIRECTORY"));
            modWorkspaceStack.Children.Add(CreateFieldLabel("Mod Engine 2 workspace used by Skin tools. " + "Select the mod root (usually ...\\Game\\mod); the toolkit detects its parts subfolder automatically."));
            string settingsModRoot = GetModEngineRootFolder();
            string settingsPartsFolder = GetPartsDestinationFolder(false);
            modWorkspaceStack.Children.Add(CreateInfoLine("Mod Folder", string.IsNullOrWhiteSpace(settingsModRoot)? "Not configured" : settingsModRoot));
            if (!string.IsNullOrWhiteSpace(settingsModRoot) && Directory.Exists(settingsModRoot)) {
                modWorkspaceStack.Children.Add(CreateInfoLine("Parts Folder", Directory.Exists(settingsPartsFolder)? settingsPartsFolder : "Not present yet"));
            }
            StackPanel modWorkspaceButtons = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0)
            };
            Button setModWorkspaceButton = new Button {
                Content = "Select Mod Engine 2 Folder...", Style = (Style) FindResource("ActionButton")
            };
            setModWorkspaceButton.Click += (_, _) => {
                ChooseActiveModFolder();
                BuildSettingsPathsPage();
            };
            Button openModWorkspaceButton = new Button {
                Content = "Open Folder", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0), IsEnabled =!string.IsNullOrWhiteSpace(settingsModRoot) && Directory.Exists(settingsModRoot)
            };
            openModWorkspaceButton.Click += (_, _) => {
                OpenFolderInExplorer(settingsModRoot);
            };
            modWorkspaceButtons.Children.Add(setModWorkspaceButton);
            modWorkspaceButtons.Children.Add(openModWorkspaceButton);
            modWorkspaceStack.Children.Add(modWorkspaceButtons);
            modWorkspaceCard.Child = modWorkspaceStack;
            root.Children.Add(modWorkspaceCard);
            Border dataCard = CreateCard();
            StackPanel dataStack = new StackPanel();
            dataStack.Children.Add(CreateSectionTitle("TOOLKIT DATA"));
            string backupPath = GetInstalledModsBackupRoot();
            dataStack.Children.Add(CreateInfoLine("App Data", settingsFolder));
            dataStack.Children.Add(CreateInfoLine("Backups", backupPath));
            Button openDataButton = new Button {
                Content = "Open App Data", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
            };
            openDataButton.Click += (_, _) => {
                Directory.CreateDirectory(settingsFolder);
                OpenFolderInExplorer(settingsFolder);
            };
            dataStack.Children.Add(openDataButton);
            dataCard.Child = dataStack;
            root.Children.Add(dataCard);
            ContentHost.Children.Add(root);
            StatusText.Text = "Settings > General";
        }
        private void BuildSettingsInterfacePage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Appearance", "Customize the toolkit theme, appearance mode, and interface scale."));
            Border styleCard = CreateCard();
            styleCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel styleStack = new StackPanel();
            styleStack.Children.Add(CreateSectionTitle("THEME"));
            styleStack.Children.Add(CreateFieldLabel("Style preset"));
            ComboBox presetCombo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 18)
            };
            string[] presets = {
                "Elden Ring Style", "Moonlight Blue", "Crimson Flame", "Emerald Grace"
            };
            foreach (string preset in presets) {
                presetCombo.Items.Add(preset);
            }
            presetCombo.SelectedItem = appearancePreset;
            presetCombo.SelectionChanged += (_, _) => {
                if (presetCombo.SelectedItem is not string selected) {
                    return;
                }
                appearancePreset = selected;
                ApplyAppearanceSettings();
                SaveToolkitSettings();
                RefreshCurrentViewAfterAppearanceChange();
            };
            styleStack.Children.Add(presetCombo);
            styleStack.Children.Add(CreateFieldLabel("Appearance mode"));
            ComboBox modeCombo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 18)
            };
            modeCombo.Items.Add("Night");
            modeCombo.Items.Add("Dark");
            modeCombo.Items.Add("Light");
            modeCombo.SelectedItem = appearanceMode;
            modeCombo.SelectionChanged += (_, _) => {
                if (modeCombo.SelectedItem is not string selected) {
                    return;
                }
                appearanceMode = selected;
                ApplyAppearanceSettings();
                SaveToolkitSettings();
                RefreshCurrentViewAfterAppearanceChange();
            };
            styleStack.Children.Add(modeCombo);
            Border preview = new Border {
                Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("Accent"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(16), Margin = new Thickness(0, 4, 0, 0)
            };
            StackPanel previewStack = new StackPanel();
            previewStack.Children.Add(new TextBlock {
                Text = appearancePreset, Foreground = GetResourceBrush("AccentBright"), FontFamily = new FontFamily("Georgia"), FontSize = 18, FontWeight = FontWeights.Bold
            });
            previewStack.Children.Add(new TextBlock {
                Text = appearanceMode + " mode", Foreground = GetResourceBrush("TextSoft"), FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0)
            });
            preview.Child = previewStack;
            styleStack.Children.Add(preview);
            styleCard.Child = styleStack;
            root.Children.Add(styleCard);
            Border scaleCard = CreateCard();
            scaleCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel scaleStack = new StackPanel();
            scaleStack.Children.Add(CreateSectionTitle("USER INTERFACE"));
            scaleStack.Children.Add(CreateFieldLabel("UI scale"));
            ComboBox scaleCombo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8)
            };
            int[] scales = {
                80, 90, 100, 110, 125
            };
            foreach (int scale in scales) {
                scaleCombo.Items.Add(scale + "%");
            }
            scaleCombo.SelectedItem = uiScalePercent + "%";
            scaleCombo.SelectionChanged += (_, _) => {
                if (scaleCombo.SelectedItem is not string selected ||!int.TryParse(selected.Replace("%", ""), out int scale)) {
                    return;
                }
                uiScalePercent = scale;
                ApplyUiScaleSetting();
                SaveToolkitSettings();
            };
            scaleStack.Children.Add(scaleCombo);
            scaleStack.Children.Add(CreateFieldLabel("Changes apply immediately. Higher scales may work best with a maximized window."));
            scaleCard.Child = scaleStack;
            root.Children.Add(scaleCard);
            Border infoCard = CreateCard();
            StackPanel infoStack = new StackPanel();
            infoStack.Children.Add(CreateSectionTitle("CURRENT APPEARANCE"));
            infoStack.Children.Add(CreateInfoLine("Style", appearancePreset));
            infoStack.Children.Add(CreateInfoLine("Mode", appearanceMode));
            infoStack.Children.Add(CreateInfoLine("UI Scale", uiScalePercent + "%"));
            infoStack.Children.Add(CreateFieldLabel("Appearance settings are saved automatically."));
            infoCard.Child = infoStack;
            root.Children.Add(infoCard);
            ContentHost.Children.Add(root);
        }
        private void BuildSettingsPerformancePage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Performance", "Optional system-level tweaks for Elden Ring while the toolkit is running."));
            Border cpuCard = CreateCard();
            StackPanel cpuStack = new StackPanel();
            cpuStack.Children.Add(CreateSectionTitle("CPU AFFINITY"));
            CheckBox excludeCpuCheck = new CheckBox {
                Content = "Exclude CPU 0 from Elden Ring", IsChecked = excludeCpu0, Foreground = GetResourceBrush("TextMain"), Margin = new Thickness(0, 8, 0, 0)
            };
            cpuStack.Children.Add(excludeCpuCheck);
            cpuStack.Children.Add(CreateFieldLabel("Enable this once and leave it on. You can turn it on before or after launching Elden Ring."));
            cpuStack.Children.Add(CreateFieldLabel("When enabled, Elden Ring Toolkit automatically detects eldenring.exe and removes CPU 0 from the game's processor affinity. No extra Apply button is required."));
            cpuStack.Children.Add(CreateFieldLabel("If the game is not running yet, the toolkit waits for it. When the option is disabled or the toolkit closes, the original affinity is restored when possible."));
            TextBlock cpuStatus = CreateInfoLine("Status", excludeCpu0? "Enabled — automatic monitoring is active" : "Disabled");
            cpuStatus.Margin = new Thickness(0, 12, 0, 0);
            cpuStack.Children.Add(cpuStatus);
            excludeCpuCheck.Checked += (_, _) => {
                excludeCpu0 = true;
                SaveToolkitSettings();
                UpdateCpuAffinityMonitoring();
                cpuStatus.Text = "Status: Enabled — automatic monitoring is active";
            };
            excludeCpuCheck.Unchecked += (_, _) => {
                excludeCpu0 = false;
                SaveToolkitSettings();
                UpdateCpuAffinityMonitoring();
                cpuStatus.Text = "Status: Disabled";
            };
            cpuCard.Child = cpuStack;
            root.Children.Add(cpuCard);
            Border noteCard = CreateCard();
            noteCard.Margin = new Thickness(0, 16, 0, 0);
            StackPanel noteStack = new StackPanel();
            noteStack.Children.Add(CreateSectionTitle("HOW TO USE"));
            noteStack.Children.Add(CreateInfoLine("1", "Turn on Exclude CPU 0 from Elden Ring."));
            noteStack.Children.Add(CreateInfoLine("2", "Launch Elden Ring normally, or keep playing if it is already open."));
            noteStack.Children.Add(CreateInfoLine("3", "Nothing else is required. The toolkit handles the affinity automatically while it remains open."));
            noteCard.Child = noteStack;
            root.Children.Add(noteCard);
            ContentHost.Children.Add(root);
        }
        private void ApplyUiScaleSetting() {
            if (ToolkitRoot == null) {
                return;
            }
            double scale = uiScalePercent / 100.0;
            ToolkitRoot.LayoutTransform = new ScaleTransform(scale, scale);
        }
        private void UpdateCpuAffinityMonitoring() {
            if (excludeCpu0) {
                if (cpuAffinityTimer == null) {
                    cpuAffinityTimer = new DispatcherTimer {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    cpuAffinityTimer.Tick += (_, _) => {
                        ApplyCpu0ExclusionToRunningGame();
                    };
                }
                cpuAffinityTimer.Start();
                ApplyCpu0ExclusionToRunningGame();
                return;
            }
            if (cpuAffinityTimer != null) {
                cpuAffinityTimer.Stop();
            }
            RestoreTrackedProcessAffinity();
        }
        private bool ApplyCpu0ExclusionToRunningGame() {
            bool applied = false;
            Process[] processes = Process.GetProcessesByName("eldenring");
            foreach (Process process in processes) {
                try {
                    IntPtr current = process.ProcessorAffinity;
                    long currentMask = current.ToInt64();
                    long newMask = currentMask &~1L;
                    if (newMask == 0) {
                        continue;
                    }
                    if (!originalProcessAffinity.ContainsKey(process.Id)) {
                        originalProcessAffinity[process.Id] = current;
                    }
                    if (currentMask != newMask) {
                        process.ProcessorAffinity = new IntPtr(newMask);
                    }
                    applied = true;
                } catch {
                } finally {
                    process.Dispose();
                }
            }
            return applied;
        }
        private void RestoreTrackedProcessAffinity() {
            foreach (KeyValuePair<int, IntPtr> entry in originalProcessAffinity.ToList()) {
                try {
                    Process process = Process.GetProcessById(entry.Key);
                    process.ProcessorAffinity = entry.Value;
                    process.Dispose();
                } catch {
                }
            }
            originalProcessAffinity.Clear();
        }
        private void MainWindow_Closed(object?sender, EventArgs e) {
            if (cpuAffinityTimer != null) {
                cpuAffinityTimer.Stop();
            }
            RestoreTrackedProcessAffinity();
        }
        private static string BlendHexColors(string foreground, string background, double foregroundAmount) {
            Color fg = (Color) ColorConverter.ConvertFromString(foreground);
            Color bg = (Color) ColorConverter.ConvertFromString(background);
            byte r = (byte) Math.Round(fg.R * foregroundAmount + bg.R * (1.0 - foregroundAmount));
            byte g = (byte) Math.Round(fg.G * foregroundAmount + bg.G * (1.0 - foregroundAmount));
            byte b = (byte) Math.Round(fg.B * foregroundAmount + bg.B * (1.0 - foregroundAmount));
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }
        private void LoadToolkitSettings() {
            try {
                if (!File.Exists(ToolkitSettingsFilePath)) {
                    return;
                }
                foreach (string line in File.ReadAllLines(ToolkitSettingsFilePath)) {
                    string[] parts = line.Split('=', 2);
                    if (parts.Length != 2) {
                        continue;
                    }
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    if (key.Equals("InstalledModsPageSize", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int pageSize) && (pageSize == 15 || pageSize == 25 || pageSize == 50)) {
                        installedModsPageSize = pageSize;
                    }
                    if (key.Equals("AppearancePreset", StringComparison.OrdinalIgnoreCase) && (value == "Elden Ring Style" || value == "Moonlight Blue" || value == "Crimson Flame" || value == "Emerald Grace")) {
                        appearancePreset = value;
                    }
                    if (key.Equals("AppearanceMode", StringComparison.OrdinalIgnoreCase) && (value == "Dark" || value == "Light" || value == "Night")) {
                        appearanceMode = value;
                    }
                    if (key.Equals("UiScalePercent", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int savedScale) && (savedScale == 80 || savedScale == 90 || savedScale == 100 || savedScale == 110 || savedScale == 125)) {
                        uiScalePercent = savedScale;
                    }
                    if (key.Equals("ExcludeCpu0", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out bool savedExcludeCpu0)) {
                        excludeCpu0 = savedExcludeCpu0;
                    }
                    if (key.Equals("GameDataFolder", StringComparison.OrdinalIgnoreCase)) {
                        gameDataFolder = value;
                    }
                }
            } catch {
            }
        }
        private void SaveToolkitSettings() {
            try {
                Directory.CreateDirectory(settingsFolder);
                File.WriteAllLines(ToolkitSettingsFilePath, new[] {
                    "InstalledModsPageSize=" + installedModsPageSize, "AppearancePreset=" + appearancePreset, "AppearanceMode=" + appearanceMode, "UiScalePercent=" + uiScalePercent, "ExcludeCpu0=" + excludeCpu0, "GameDataFolder=" + gameDataFolder
                });
            } catch {
            }
        }
        private void ApplyAppearanceSettings() {
            bool light = appearanceMode == "Light";
            bool night = appearanceMode == "Night";
            string bgMain;
            string bgPanel;
            string bgPanel2;
            string bgHover;
            string bgPressed;
            string borderMain;
            string borderSoft;
            string textMain;
            string textSoft;
            string textMuted;
            string accent;
            string accentBright;
            string dropBorder;
            string dropHover;
            string dropSelected;
            string dropArrow;
            string dropItemText;
            if (light) {
                bgMain = "#F3F0E8";
                bgPanel = "#EAE5DA";
                bgPanel2 = "#E1DBCF";
                bgHover = "#D8D1C3";
                bgPressed = "#CDC5B7";
                borderMain = "#B8B0A2";
                borderSoft = "#CBC3B5";
                textMain = "#201D18";
                textSoft = "#5C554B";
                textMuted = "#777064";
                dropHover = "#D7DFE2";
                dropSelected = "#C7D4D9";
                dropItemText = "#201D18";
            } else if (night) {
                bgMain = "#000000";
                bgPanel = "#030303";
                bgPanel2 = "#090909";
                bgHover = "#111315";
                bgPressed = "#181B1E";
                borderMain = "#25292D";
                borderSoft = "#181B1E";
                textMain = "#F3F3F3";
                textSoft = "#B6BABE";
                textMuted = "#858B91";
                dropHover = "#10161A";
                dropSelected = "#18242A";
                dropItemText = "#F0F4F6";
            } else {
                bgMain = "#0D1012";
                bgPanel = "#14181B";
                bgPanel2 = "#181D20";
                bgHover = "#20262A";
                bgPressed = "#292F33";
                borderMain = "#343B3F";
                borderSoft = "#2A3034";
                textMain = "#E8E3D6";
                textSoft = "#AAA69C";
                textMuted = "#7F837E";
                dropHover = "#1D2C34";
                dropSelected = "#27414C";
                dropItemText = "#DDE5E8";
            }
            switch (appearancePreset) {
                case "Moonlight Blue" : accent = light? "#376F9E" : (night? "#5AA7E8" : "#5C9BD3");
                accentBright = light? "#275A84" : (night? "#8AC7F6" : "#82B9E8");
                dropBorder = light? "#6D94AE" : (night? "#345F7A" : "#416D89");
                dropArrow = light? "#376F9E" : (night? "#87C3EA" : "#7EB1D2");
                break;
                case "Crimson Flame" : accent = light? "#A9473D" : (night? "#D85E53" : "#C65D52");
                accentBright = light? "#8F342D" : (night? "#F18A7F" : "#E07B6F");
                dropBorder = light? "#A47772" : (night? "#743E39" : "#7B4C47");
                dropArrow = light? "#A9473D" : (night? "#F08B80" : "#D98479");
                break;
                case "Emerald Grace" : accent = light? "#3D7E63" : (night? "#55A97D" : "#5A9C7D");
                accentBright = light? "#2E684F" : (night? "#83D4AA" : "#79B99A");
                dropBorder = light? "#6F9482" : (night? "#38634F" : "#466F5D");
                dropArrow = light? "#3D7E63" : (night? "#84D0AA" : "#7CB79C");
                break;
                default : appearancePreset = "Elden Ring Style";
                accent = light? "#9A6F22" : (night? "#C59637" : "#B78D36");
                accentBright = light? "#7A5517" : (night? "#E1B85D" : "#D0A64F");
                dropBorder = light? "#668493" : (night? "#3B5B68" : "#365A68");
                dropArrow = light? "#477282" : (night? "#82B2C6" : "#78A8BE");
                break;
            }
            dropBorder = BlendHexColors(accentBright, bgPanel2, light? 0.58 : 0.52);
            dropArrow = accentBright;
            dropHover = BlendHexColors(accent, bgPanel2, light? 0.18 : 0.24);
            dropSelected = BlendHexColors(accentBright, bgPanel2, light? 0.30 : 0.38);
            SetResourceBrushColor("BgMain", bgMain);
            SetResourceBrushColor("BgPanel", bgPanel);
            SetResourceBrushColor("BgPanel2", bgPanel2);
            SetResourceBrushColor("BgHover", bgHover);
            SetResourceBrushColor("BgPressed", bgPressed);
            SetResourceBrushColor("BorderMain", borderMain);
            SetResourceBrushColor("BorderSoft", borderSoft);
            SetResourceBrushColor("TextMain", textMain);
            SetResourceBrushColor("TextSoft", textSoft);
            SetResourceBrushColor("TextMuted", textMuted);
            SetResourceBrushColor("Accent", accent);
            SetResourceBrushColor("AccentBright", accentBright);
            SetResourceBrushColor("DropBorder", dropBorder);
            SetResourceBrushColor("DropHover", dropHover);
            SetResourceBrushColor("DropSelected", dropSelected);
            SetResourceBrushColor("DropArrow", dropArrow);
            SetResourceBrushColor("DropItemText", dropItemText);
            SetResourceBrushColor("AccentSelection", BlendHexColors(accent, bgPanel2, light? 0.24 : 0.22));
            SetResourceBrushColor("AccentButton", BlendHexColors(accent, bgPanel2, light? 0.56 : 0.46));
            SetResourceBrushColor("AccentButtonHover", BlendHexColors(accentBright, bgPanel2, light? 0.66 : 0.58));
            SetBrushColor(ref inactiveTopTextBrush, light? "#5C554B" : (night? "#A7ADB3" : "#AEB4BE"));
            SetBrushColor(ref activeTopTextBrush, textMain);
            SetBrushColor(ref activeLineBrush, accent);
            SetBrushColor(ref sidebarTitleBrush, light? "#6D665D" : (night? "#8D949B" : "#777E8A"));
            SetBrushColor(ref sidebarNormalTextBrush, light? "#4F4941" : (night? "#C2C7CC" : "#B8BEC8"));
            SetBrushColor(ref sidebarActiveTextBrush, textMain);
            SetBrushColor(ref sidebarActiveBackgroundBrush, light? "#D7D0C3" : (night? "#17191C" : "#2D313A"));
            SetBrushColor(ref separatorBrush, light? "#C7BFB1" : (night? "#25292D" : "#363A45"));
            SetBrushColor(ref normalTextBrush, textMain);
            SetBrushColor(ref mutedTextBrush, light? "#6E675D" : (night? "#B0B5BA" : "#9B978C"));
            SetBrushColor(ref warningBrush, accentBright);
            Background = GetResourceBrush("BgMain");
            InvalidateVisual();
        }
        private void SetResourceBrushColor(string key, string color) {
            Color newColor = (Color) ColorConverter.ConvertFromString(color);
            object?existing = TryFindResource(key);
            if (existing is SolidColorBrush existingBrush &&!existingBrush.IsFrozen) {
                existingBrush.Color = newColor;
                return;
            }
            Resources[key] = new SolidColorBrush(newColor);
        }
        private void SetBrushColor(ref Brush brush, string color) {
            Color newColor = (Color) ColorConverter.ConvertFromString(color);
            if (brush is SolidColorBrush solid &&!solid.IsFrozen) {
                solid.Color = newColor;
                return;
            }
            brush = new SolidColorBrush(newColor);
        }
        private void RefreshCurrentViewAfterAppearanceChange() {
            Button? activeTop = currentSection switch {
                "Save" => SaveButton, "Character" => CharacterButton, "Inventory" => InventoryButton, "Presets" => PresetsButton, "Skin" => SkinButton, "About" => AboutButton, "Settings" => SettingsButton, _ => null
            };
            if (activeTop != null) {
                UpdateTopHighlight(activeTop);
            }
            foreach (UIElement element in SidebarPanel.Children) {
                if (element is TextBlock title) {
                    title.Foreground = sidebarTitleBrush;
                } else if (element is Separator separator) {
                    separator.Background = separatorBrush;
                } else if (element is Button button) {
                    bool selected = ReferenceEquals(button, activeSidebarButton);
                    button.Foreground = selected? sidebarActiveTextBrush : sidebarNormalTextBrush;
                    button.Background = selected? sidebarActiveBackgroundBrush : Brushes.Transparent;
                }
            }
            if (!string.IsNullOrWhiteSpace(currentToolName)) {
                OpenToolPage(currentToolName);
            }
            InvalidateVisual();
            UpdateLayout();
        }
        private Brush GetResourceBrush(string key) {
            return TryFindResource(key) as Brush ?? Brushes.Transparent;
        }
        private void ChooseGameDataFolder() {
            OpenFolderDialog dialog = new OpenFolderDialog {
                Title = "Select Elden Ring Game Folder"
            };
            bool?result = dialog.ShowDialog();
            if (result != true) {
                return;
            }
            string selectedFolder = dialog.FolderName;
            string exePath = Path.Combine(selectedFolder, "eldenring.exe");
            if (!File.Exists(exePath)) {
                MessageBox.Show("The selected folder does not contain eldenring.exe." + "\n\n" + "Please select the Elden Ring Game folder.", "Invalid Game Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            gameDataFolder = selectedFolder;
            SaveToolkitSettings();
            StatusText.Text = "Settings > Data Paths > Game folder updated";
            _ = StartFullGameIconPrecacheAsync(true);
            if (databaseResultsList?.SelectedItem is EldenRingItem selectedDatabaseItem) {
                UpdateDatabaseItemImage(selectedDatabaseItem);
            }
        }
        private void ChooseActiveModFolder() {
            OpenFolderDialog dialog = new OpenFolderDialog {
                Title = "Select Mod Engine 2 mod folder"
            };
            bool?result = dialog.ShowDialog();
            if (result != true) {
                return;
            }
            string selectedFolder = NormalizeModEngineRootFolder(dialog.FolderName);
            if (string.IsNullOrWhiteSpace(selectedFolder) ||!Directory.Exists(selectedFolder)) {
                MessageBox.Show("Please select a valid Mod Engine 2 mod folder.\n\n" + "Typical path:\n" + @"D:\Games\ELDEN RING\Game\mod", "Invalid Mod Engine 2 Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            gameModFolder = selectedFolder;
            SaveModFolder();
            InvalidateInstalledModSnapshot();
            ScanInstalledModFolder();
            StatusText.Text = "Settings > Paths > Mod Engine 2 folder updated";
        }
        private string NormalizeModEngineRootFolder(string?selectedFolder) {
            if (string.IsNullOrWhiteSpace(selectedFolder)) {
                return "";
            }
            string fullPath;
            try {
                fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedFolder));
            } catch {
                return selectedFolder.Trim();
            }
            DirectoryInfo info = new DirectoryInfo(fullPath);
            if (info.Name.Equals("parts", StringComparison.OrdinalIgnoreCase) && info.Parent != null) {
                return info.Parent.FullName;
            }
            if (File.Exists(Path.Combine(fullPath, "eldenring.exe"))) {
                string conventionalModFolder = Path.Combine(fullPath, "mod");
                if (Directory.Exists(conventionalModFolder)) {
                    return conventionalModFolder;
                }
            }
            string[] knownContentFolders = {
                "action", "asset", "chr", "event", "map", "material", "menu", "msg", "param", "parts", "script", "sfx"
            };
            if (info.Parent != null && knownContentFolders.Any(x => x.Equals(info.Name, StringComparison.OrdinalIgnoreCase))) {
                string parent = info.Parent.FullName;
                bool parentLooksLikeModRoot = File.Exists(Path.Combine(parent, "regulation.bin")) || Directory.Exists(Path.Combine(parent, "parts")) || knownContentFolders.Count(x => Directory.Exists(Path.Combine(parent, x))) >= 2;
                if (parentLooksLikeModRoot) {
                    return parent;
                }
            }
            return fullPath;
        }
        private string GetModEngineRootFolder() {
            return NormalizeModEngineRootFolder(gameModFolder);
        }
        private void OpenFolderInExplorer(string folder) {
            if (string.IsNullOrWhiteSpace(folder) ||!Directory.Exists(folder)) {
                return;
            }
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = "explorer.exe", Arguments = "\"" + folder + "\"", UseShellExecute = true
                });
            } catch {
            }
        }
        private const string InventoryCatalogUrl = "https://raw.githubusercontent.com/Deskete/EldenRingResources/main/ITEM%20IDS%20Elden%20Ring.txt";
        private List<InventoryCatalogItem>? inventoryCatalogCache;
        private void ApplyInventorySearchBoxTheme(TextBox box) {
            box.ClearValue(FrameworkElement.StyleProperty);
            box.Background = GetResourceBrush("BgPanel");
            box.Foreground = GetResourceBrush("TextMain");
            TextElement.SetForeground(box, GetResourceBrush("TextMain"));
            box.BorderBrush = GetResourceBrush("BorderMain");
            box.BorderThickness = new Thickness(1);
            box.CaretBrush = GetResourceBrush("AccentBright");
            box.SelectionBrush = GetResourceBrush("AccentSelection");
            box.SelectionOpacity = 0.45;
            box.Padding = new Thickness(10, 2, 10, 2);
            box.FocusVisualStyle = null;
            box.SnapsToDevicePixels = false;
            box.UseLayoutRounding = true;
            box.Resources[SystemColors.HighlightBrushKey] = GetResourceBrush("AccentSelection");
            box.Resources[SystemColors.HighlightTextBrushKey] = GetResourceBrush("TextMain");
            box.Resources[SystemColors.ControlTextBrushKey] = GetResourceBrush("TextMain");
            static Border? FindNativeTextBoxBorder(DependencyObject parent) {
                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i<count; i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    if (child is Border border) return border;
                    Border? nested = FindNativeTextBoxBorder(child);
                    if (nested != null) return nested;
                }
                return null;
            }
            void RoundNativeTextBoxChrome() {
                box.ApplyTemplate();
                Border? nativeBorder = FindNativeTextBoxBorder(box);
                if (nativeBorder == null) return;
                nativeBorder.CornerRadius = new CornerRadius(7);
                nativeBorder.SnapsToDevicePixels = false;
                nativeBorder.UseLayoutRounding = true;
            }
            void RefreshNativeSearchBrushes() {
                box.Foreground = GetResourceBrush("TextMain");
                TextElement.SetForeground(box, GetResourceBrush("TextMain"));
                box.Background = GetResourceBrush("BgPanel");
                box.CaretBrush = GetResourceBrush("AccentBright");
                box.BorderBrush = box.IsKeyboardFocusWithin? GetResourceBrush("AccentBright") : GetResourceBrush("BorderMain");
                RoundNativeTextBoxChrome();
            }
            box.Loaded += (_, _) => RefreshNativeSearchBrushes();
            box.GotKeyboardFocus += (_, _) => RefreshNativeSearchBrushes();
            box.LostKeyboardFocus += (_, _) => RefreshNativeSearchBrushes();
            box.SizeChanged += (_, _) => RoundNativeTextBoxChrome();
            box.TextChanged += (_, _) => {
                if (!ReferenceEquals(box.Foreground, GetResourceBrush("TextMain"))) box.Foreground = GetResourceBrush("TextMain");
                TextElement.SetForeground(box, GetResourceBrush("TextMain"));
            };
        }
        private static bool IsUserFacingInventoryAddItem(InventoryCatalogItem item) {
            if (!string.Equals(item.Category, "Consumables", StringComparison.OrdinalIgnoreCase)) return true;
            string name = item.Name.Trim();
            if (Regex.IsMatch(name, @"^Flask of (?:Crimson|Cerulean) Tears(?: \+\d+)?(?: \(Empty\))?$", RegexOptions.IgnoreCase)) {
                return name.Equals("Flask of Crimson Tears", StringComparison.OrdinalIgnoreCase) || name.Equals("Flask of Cerulean Tears", StringComparison.OrdinalIgnoreCase);
            }
            if (name.Equals("Flask of Wondrous Physick (Empty)", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        private void BuildInventoryPage(string toolName) {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle(toolName, "Browse, add and edit items for the selected character. Held Inventory and Storage are read directly from the loaded save."));
            if (!saveSessionLoaded || string.IsNullOrWhiteSpace(loadedSavePath) ||!File.Exists(loadedSavePath)) {
                Border locked = CreateCard();
                StackPanel lockedStack = new StackPanel();
                lockedStack.Children.Add(CreateSectionTitle("SAVE REQUIRED"));
                lockedStack.Children.Add(CreateFieldLabel("Load a save from Save > Save Import first."));
                locked.Child = lockedStack;
                root.Children.Add(locked);
                ContentHost.Children.Add(root);
                return;
            }
            List<CharacterSlotInfo> slots;
            try {
                slots = GetEditableCharacterSlots();
            } catch (Exception ex) {
                Border error = CreateCard();
                StackPanel es = new StackPanel();
                es.Children.Add(CreateSectionTitle("INVENTORY UNAVAILABLE"));
                es.Children.Add(CreateFieldLabel(ex.Message));
                error.Child = es;
                root.Children.Add(error);
                ContentHost.Children.Add(root);
                ShowToast("Could not read inventory • " + ex.Message, ToastKind.Warning);
                return;
            }
            if (slots.Count == 0) {
                Border empty = CreateCard();
                StackPanel es = new StackPanel();
                es.Children.Add(CreateSectionTitle("NO CHARACTERS"));
                es.Children.Add(CreateFieldLabel("No active character slots were found in this save."));
                empty.Child = es;
                root.Children.Add(empty);
                ContentHost.Children.Add(root);
                return;
            }
            ComboBox characterCombo = CreateCharacterEditorSlotCombo(slots);
            root.Children.Add(BuildCharacterEditorSelectorCard(characterCombo));
            Grid workspace = new Grid {
                Margin = new Thickness(0, 14, 0, 0)
            };
            workspace.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(0.74, GridUnitType.Star)
            });
            workspace.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1.26, GridUnitType.Star)
            });
            Border addCard = CreateCard();
            addCard.Margin = new Thickness(0, 0, 10, 0);
            StackPanel addStack = new StackPanel();
            addStack.Children.Add(CreateSectionTitle("ADD ITEMS"));
            Grid addSearchRow = new Grid {
                Margin = new Thickness(0, 8, 0, 7)
            };
            addSearchRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            addSearchRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(76)
            });
            TextBox addSearch = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search items in this category"
            };
            ApplyInventorySearchBoxTheme(addSearch);
            Button clearAddSearch = new Button {
                Content = "Clear", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(7, 0, 0, 0), Height = 34, FontSize = 12
            };
            Grid.SetColumn(clearAddSearch, 1);
            addSearchRow.Children.Add(addSearch);
            addSearchRow.Children.Add(clearAddSearch);
            addStack.Children.Add(addSearchRow);
            Button visualPickerButton = new Button {
                Content = "Add Item...", Style = (Style) FindResource("PrimaryButton"), Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 9), FontSize = 12.5
            };
            addStack.Children.Add(visualPickerButton);
            ListBox catalogList = new ListBox {
                MinHeight = 310, MaxHeight = 420, Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), Foreground = GetResourceBrush("TextMain")
            };
            addStack.Children.Add(catalogList);
            TextBlock catalogHint = CreateFieldLabel("Select an item here for a quick preview, or use Add Item for the full icon grid and add controls.");
            catalogHint.Margin = new Thickness(0, 8, 0, 0);
            addStack.Children.Add(catalogHint);
            addCard.Child = addStack;
            workspace.Children.Add(addCard);
            Border currentCard = CreateCard();
            currentCard.Margin = new Thickness(10, 0, 0, 0);
            StackPanel currentStack = new StackPanel();
            currentStack.Children.Add(CreateSectionTitle("CURRENT INVENTORY"));
            Grid currentTools = new Grid {
                Margin = new Thickness(0, 8, 0, 7)
            };
            currentTools.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(0.82, GridUnitType.Star)
            });
            currentTools.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1.00, GridUnitType.Star)
            });
            currentTools.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1.18, GridUnitType.Star)
            });
            currentTools.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            currentTools.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            ComboBox locationFilter = CreateInventoryCombo(new[] {
                "Inventory", "Storage", "All Locations"
            });
            locationFilter.SelectedIndex = 0;
            locationFilter.Height = 34;
            locationFilter.Margin = new Thickness(0, 0, 7, 0);
            ComboBox sortFilter = CreateInventoryCombo(new[] {
                "A-Z", "Z-A", "Quantity", "Upgrade"
            });
            sortFilter.SelectedIndex = 0;
            sortFilter.Height = 34;
            sortFilter.Margin = new Thickness(0, 0, 7, 0);
            Button visualInventoryButton = new Button {
                Content = "Edit Inventory...", Style = (Style) FindResource("PrimaryButton"), Height = 34, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Focusable = false, FocusVisualStyle = null
            };
            TextBox currentSearch = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, ToolTip = "Filter current inventory"
            };
            ApplyInventorySearchBoxTheme(currentSearch);
            Grid.SetColumn(locationFilter, 0);
            Grid.SetRow(locationFilter, 0);
            Grid.SetColumn(sortFilter, 1);
            Grid.SetRow(sortFilter, 0);
            Grid.SetColumn(visualInventoryButton, 2);
            Grid.SetRow(visualInventoryButton, 0);
            Grid.SetColumn(currentSearch, 0);
            Grid.SetColumnSpan(currentSearch, 3);
            Grid.SetRow(currentSearch, 1);
            currentTools.Children.Add(locationFilter);
            currentTools.Children.Add(sortFilter);
            currentTools.Children.Add(visualInventoryButton);
            currentTools.Children.Add(currentSearch);
            currentStack.Children.Add(currentTools);
            TextBlock inventorySummary = CreateFieldLabel("Reading inventory...");
            inventorySummary.Margin = new Thickness(0, 2, 0, 8);
            currentStack.Children.Add(inventorySummary);
            ListBox inventoryList = new ListBox {
                MinHeight = 310, MaxHeight = 420, Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), Foreground = GetResourceBrush("TextMain")
            };
            currentStack.Children.Add(inventoryList);
            StackPanel inventoryButtons = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0)
            };
            Button removeSelected = CreateInventoryActionButton("Remove", false);
            Button quantitySelected = CreateInventoryActionButton("Set Quantity", false);
            Button upgradeSelected = CreateInventoryActionButton("Set Upgrade", false);
            Button affinitySelected = CreateInventoryActionButton("Set Affinity", false);
            Button ashSelected = CreateInventoryActionButton("Set AoW", false);
            inventoryButtons.Children.Add(removeSelected);
            inventoryButtons.Children.Add(quantitySelected);
            inventoryButtons.Children.Add(upgradeSelected);
            inventoryButtons.Children.Add(affinitySelected);
            inventoryButtons.Children.Add(ashSelected);
            currentStack.Children.Add(inventoryButtons);
            currentCard.Child = currentStack;
            Grid.SetColumn(currentCard, 1);
            workspace.Children.Add(currentCard);
            root.Children.Add(workspace);
            Border noteCard = CreateCard();
            noteCard.Margin = new Thickness(0, 14, 0, 0);
            StackPanel noteStack = new StackPanel();
            TextBlock note = new TextBlock {
                Text = "NOTE: Upgrade, Affinity and Ash of War are weapon-only controls. Armor, talismans, consumables, spells, Spirit Ashes, Ashes of War and key items disable them automatically. All writes create a safety backup and recalculate the slot checksum.", Foreground = GetResourceBrush("AccentBright"), TextWrapping = TextWrapping.Wrap
            };
            noteStack.Children.Add(note);
            noteCard.Child = noteStack;
            root.Children.Add(noteCard);
            List<InventoryCatalogItem> catalog = new List<InventoryCatalogItem>();
            InventorySnapshot? currentSnapshot = null;
            Style CreateInventoryListItemStyle() {
                Style style = new Style(typeof(ListBoxItem));
                style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextMain")));
                style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
                style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
                style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
                style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
                style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
                FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
                border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                presenter.SetBinding(TextElement.ForegroundProperty, new System.Windows.Data.Binding("Foreground") {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
                });
                border.AppendChild(presenter);
                ControlTemplate template = new ControlTemplate(typeof(ListBoxItem)) {
                    VisualTree = border
                };
                style.Setters.Add(new Setter(Control.TemplateProperty, template));
                Trigger hover = new Trigger {
                    Property = ListBoxItem.IsMouseOverProperty, Value = true
                };
                hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropHover")));
                hover.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("DropBorder")));
                style.Triggers.Add(hover);
                Trigger selected = new Trigger {
                    Property = ListBoxItem.IsSelectedProperty, Value = true
                };
                selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropSelected")));
                selected.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBright")));
                selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextMain")));
                style.Triggers.Add(selected);
                return style;
            }
            Style inventoryListItemStyle = CreateInventoryListItemStyle();
            catalogList.ItemContainerStyle = inventoryListItemStyle;
            inventoryList.ItemContainerStyle = inventoryListItemStyle;
            void StyleListBoxItem(ListBoxItem row, bool selected = false) {
                row.SetResourceReference(Control.ForegroundProperty, "TextMain");
                row.Padding = new Thickness(10, 7, 10, 7);
                row.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                row.ClearValue(Control.BackgroundProperty);
                row.ClearValue(Control.BorderBrushProperty);
            }
            void RenderCatalog() {
                catalogList.Items.Clear();
                string q = addSearch.Text.Trim();
                foreach (InventoryCatalogItem item in catalog.Where(x => x.Category == NormalizeInventoryToolCategory(toolName)).Where(IsUserFacingInventoryAddItem).Where(x => string.IsNullOrWhiteSpace(q) || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(350)) {
                    ListBoxItem row = new ListBoxItem {
                        Content = item.Name, Tag = item, ToolTip = item.Name + "  •  Param ID " + item.ParamId
                    };
                    StyleListBoxItem(row);
                    catalogList.Items.Add(row);
                }
            }
            void RenderCurrent() {
                inventoryList.Items.Clear();
                if (currentSnapshot == null) return;
                string q = currentSearch.Text.Trim();
                string location = locationFilter.SelectedItem?.ToString() ?? "Inventory";
                string sort = sortFilter.SelectedItem?.ToString() ?? "A-Z";
                string category = NormalizeInventoryToolCategory(toolName);
                IEnumerable<InventoryEntryInfo> entries = currentSnapshot.Items.Where(x => x.Category == category).Where(x => location == "All Locations" || x.Location == (location == "Inventory"? "Held" : location)).Where(x => string.IsNullOrWhiteSpace(q) || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
                entries = sort switch {
                    "Z-A" => entries.OrderByDescending(x => x.Name), "Quantity" => entries.OrderByDescending(x => x.Quantity).ThenBy(x => x.Name), "Upgrade" => entries.OrderByDescending(x => x.Upgrade).ThenBy(x => x.Name), _ => entries.OrderBy(x => x.Name)
                };
                foreach (InventoryEntryInfo entry in entries) {
                    string displayLocation = entry.Location == "Held"? "Inventory" : entry.Location;
                    string suffix = entry.Category == "Weapons"?$"  •  +{entry.Upgrade}  •  Qty {entry.Quantity}  •  {displayLocation}" : $"  •  Qty {entry.Quantity}  •  {displayLocation}";
                    ListBoxItem row = new ListBoxItem {
                        Content = GetInventoryEntryDisplayName(entry) + suffix, Tag = entry, ToolTip = $"Handle 0x{entry.Handle:X8}  •  Param {entry.ParamId}"
                    };
                    StyleListBoxItem(row);
                    inventoryList.Items.Add(row);
                }
            }
            async void RefreshEverything() {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (selected == null) {
                    inventorySummary.Text = "Select a character.";
                    visualPickerButton.IsEnabled = false;
                    visualInventoryButton.IsEnabled = false;
                    currentSnapshot = null;
                    RenderCurrent();
                    return;
                }
                selectedCharacterSlotIndex = selected.Index;
                try {
                    currentSnapshot = ReadInventorySnapshot(loadedSavePath, selected.Index);
                    if (catalog.Count == 0) catalog = await LoadInventoryCatalogAsync();
                    ApplyCatalogNames(currentSnapshot, catalog);
                    int heldCount = currentSnapshot.Items.Count(x => x.Location == "Held");
                    int storageCount = currentSnapshot.Items.Count(x => x.Location == "Storage");
                    int categoryCount = currentSnapshot.Items.Count(x => x.Category == NormalizeInventoryToolCategory(toolName));
                    inventorySummary.Text = $"{toolName}: {categoryCount:N0}  •  Inventory: {heldCount:N0}  •  Storage: {storageCount:N0}";
                    visualPickerButton.IsEnabled = true;
                    visualInventoryButton.IsEnabled = true;
                    RenderCatalog();
                    RenderCurrent();
                } catch (Exception ex) {
                    currentSnapshot = null;
                    inventorySummary.Text = "Inventory could not be located safely: " + ex.Message;
                    visualPickerButton.IsEnabled = false;
                    visualInventoryButton.IsEnabled = false;
                    catalogList.Items.Clear();
                    inventoryList.Items.Clear();
                    ShowToast("Could not locate inventory • " + ex.Message, ToastKind.Warning);
                }
            }
            void UpdateSelectedButtons() {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                bool has = entry != null;
                removeSelected.IsEnabled = has;
                quantitySelected.IsEnabled = has && entry!.Category != "Weapons" && entry.Category != "Armor" && entry.Category != "Talismans";
                upgradeSelected.IsEnabled = has && entry!.Category == "Weapons";
                affinitySelected.IsEnabled = has && entry!.Category == "Weapons" && entry.SupportsAffinity;
                ashSelected.IsEnabled = has && entry!.Category == "Weapons" && entry.SupportsAshOfWar;
            }
            characterCombo.SelectionChanged += (_, _) => RefreshEverything();
            addSearch.TextChanged += (_, _) => RenderCatalog();
            clearAddSearch.Click += (_, _) => addSearch.Clear();
            currentSearch.TextChanged += (_, _) => RenderCurrent();
            locationFilter.SelectionChanged += (_, _) => RenderCurrent();
            sortFilter.SelectionChanged += (_, _) => RenderCurrent();
            inventoryList.SelectionChanged += (_, _) => UpdateSelectedButtons();
            visualPickerButton.Click += async(_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (selected == null) return;
                await OpenInventoryAddWindowAsync(selected.Index, toolName);
                RefreshEverything();
            };
            catalogList.MouseDoubleClick += async(_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (selected == null || catalogList.SelectedItem == null) return;
                await OpenInventoryAddWindowAsync(selected.Index, toolName);
                RefreshEverything();
            };
            visualInventoryButton.Click += async(_, _) => {
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (selected == null) return;
                await OpenVisualInventoryWindowAsync(selected.Index, toolName);
                RefreshEverything();
            };
            removeSelected.Click += (_, _) => {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (entry == null || selected == null) return;
                if (MessageBox.Show($"Remove {entry.Name} from {entry.Location}?", "Remove Item", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try {
                    RemoveInventoryEntry(loadedSavePath, selected.Index, entry);
                    ShowToast("Item removed", ToastKind.Success);
                    RefreshEverything();
                } catch (Exception ex) {
                    ShowToast("Could not remove item • " + ex.Message, ToastKind.Warning);
                }
            };
            quantitySelected.Click += (_, _) => {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (entry == null || selected == null) return;
                string?value = ShowInventoryValueDialog(this, "Set Quantity", "Quantity (1–9,999)", entry.Quantity.ToString(), null);
                if (!uint.TryParse(value, out uint qty) || qty<1 || qty> 9999) return;
                try {
                    UpdateInventoryQuantity(loadedSavePath, selected.Index, entry, qty);
                    ShowToast("Quantity updated", ToastKind.Success);
                    RefreshEverything();
                } catch (Exception ex) {
                    ShowToast("Could not update quantity • " + ex.Message, ToastKind.Warning);
                }
            };
            upgradeSelected.Click += (_, _) => {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (entry == null || selected == null) return;
                int maxUpgrade = Math.Max(0, entry.MaxUpgrade);
                string?value = ShowInventoryValueDialog(this, "Set Upgrade", $"Upgrade level (0–{maxUpgrade})", entry.Upgrade.ToString(), null);
                if (string.IsNullOrWhiteSpace(value) ||!int.TryParse(value.TrimStart('+'), out int upgrade)) return;
                if (!ValidateInventoryWeaponUpgrade(entry.Name, upgrade, entry.MaxUpgrade)) return;
                try {
                    UpdateInventoryWeaponVariant(loadedSavePath, selected.Index, entry, upgrade, entry.Affinity);
                    ShowToast("Weapon upgrade updated", ToastKind.Success);
                    RefreshEverything();
                } catch (Exception ex) {
                    ShowToast("Could not update upgrade • " + ex.Message, ToastKind.Warning);
                }
            };
            affinitySelected.Click += (_, _) => {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (entry == null || selected == null) return;
                string[] affinities = GetInventoryAffinityNames();
                int currentAffinity = Math.Clamp(entry.Affinity, 0, affinities.Length - 1);
                string?value = ShowInventoryAffinityDialog(this, affinities[currentAffinity]);
                int affinity = Array.FindIndex(affinities, x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (affinity<0) return;
                try {
                    UpdateInventoryWeaponVariant(loadedSavePath, selected.Index, entry, entry.Upgrade, affinity);
                    ShowToast("Weapon affinity updated", ToastKind.Success);
                    RefreshEverything();
                } catch (Exception ex) {
                    ShowToast("Could not update affinity • " + ex.Message, ToastKind.Warning);
                }
            };
            ashSelected.Click += async(_, _) => {
                InventoryEntryInfo? entry = (inventoryList.SelectedItem as ListBoxItem)?.Tag as InventoryEntryInfo;
                CharacterSlotInfo? selected = GetSelectedSlotInfo(characterCombo);
                if (entry == null || selected == null ||!entry.SupportsAshOfWar) return;
                InventoryCatalogItem? weaponMeta = FindCatalogItemForEntry(entry, catalog);
                AshOfWarSelection choice = ShowInventoryAshOfWarDialog(this, entry, weaponMeta, catalog);
                if (!choice.Accepted) return;
                try {
                    UpdateInventoryWeaponAshOfWar(loadedSavePath, selected.Index, entry, choice.AshOfWar);
                    ShowToast(choice.AshOfWar == null? "Ash of War cleared" : "Ash of War updated", ToastKind.Success);
                    RefreshEverything();
                } catch (Exception ex) {
                    ShowToast("Could not update Ash of War • " + ex.Message, ToastKind.Warning);
                }
            };
            RefreshEverything();
            ContentHost.Children.Add(root);
            StatusText.Text = "Inventory > " + toolName;
        }
        private async System.Threading.Tasks.Task OpenInventoryAddWindowAsync(int slotIndex, string initialCategory) {
            Window window = CreateInventoryToolWindow("Add Item", 900, 720);
            Grid root = new Grid {
                Margin = new Thickness(14)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            Grid header = new Grid {
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            header.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(170)
            });
            header.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(92)
            });
            TextBox searchBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Search items"
            };
            ApplyInventorySearchBoxTheme(searchBox);
            ComboBox categoryBox = CreateInventoryCombo(new[] {
                "Weapons", "Armor", "Talismans", "Consumables", "Sorceries", "Incantations", "Spirit Ashes", "Ashes of War", "Key Items"
            });
            categoryBox.SelectedItem = NormalizeInventoryToolCategory(initialCategory);
            categoryBox.Margin = new Thickness(0, 0, 8, 0);
            Button closeButton = new Button {
                Content = "Close", Style = (Style) FindResource("ActionButton"), Width = 84, Height = 34, FontSize = 12, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, Focusable = false, FocusVisualStyle = null
            };
            closeButton.Click += (_, _) => window.Close();
            Grid.SetColumn(searchBox, 0);
            Grid.SetColumn(categoryBox, 1);
            Grid.SetColumn(closeButton, 2);
            header.Children.Add(searchBox);
            header.Children.Add(categoryBox);
            header.Children.Add(closeButton);
            Grid.SetRow(header, 0);
            root.Children.Add(header);
            ScrollViewer scroller = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            WrapPanel cards = new WrapPanel {
                Orientation = Orientation.Horizontal
            };
            scroller.Content = cards;
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);
            Border footer = CreateCard();
            footer.Margin = new Thickness(0, 12, 0, 0);
            Grid footerGrid = new Grid();
            for (int i = 0; i<4; i++) footerGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            footerGrid.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            footerGrid.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            footerGrid.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            footerGrid.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            TextBlock selectedText = CreateFieldLabel("No item selected");
            selectedText.FontWeight = FontWeights.SemiBold;
            selectedText.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetColumnSpan(selectedText, 4);
            footerGrid.Children.Add(selectedText);
            TextBox quantityBox = new TextBox {
                Text = "1", Style = (Style) FindResource("DarkTextBox"), MinWidth = 100, Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), VerticalContentAlignment = VerticalAlignment.Center
            };
            ApplyInventorySearchBoxTheme(quantityBox);
            ComboBox upgradeBox = CreateInventoryCombo(Enumerable.Range(0, 26).Select(x => "+" + x));
            upgradeBox.SelectedIndex = 0;
            ComboBox affinityBox = CreateInventoryAffinityCombo();
            affinityBox.SelectedIndex = 0;
            ComboBox locationBox = CreateInventoryCombo(new[] {
                "Inventory", "Storage"
            });
            locationBox.SelectedIndex = 0;
            footerGrid.Children.Add(BuildInventoryField("Quantity", quantityBox, 0, 1));
            footerGrid.Children.Add(BuildInventoryField("Upgrade", upgradeBox, 1, 1));
            footerGrid.Children.Add(BuildInventoryField("Affinity", affinityBox, 2, 1));
            footerGrid.Children.Add(BuildInventoryField("Location", locationBox, 3, 1));
            Grid ashRow = new Grid {
                Margin = new Thickness(0, 11, 0, 0)
            };
            ashRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            ashRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            ashRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            ashRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            TextBlock ashLabel = new TextBlock {
                Text = "Ash of War", Foreground = GetResourceBrush("TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
            };
            ashRow.Children.Add(ashLabel);
            Grid ashPreview = new Grid {
                VerticalAlignment = VerticalAlignment.Center
            };
            ashPreview.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(28)
            });
            ashPreview.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Border ashIconFrame = new Border {
                Width = 24, Height = 24, Background = GetResourceBrush("BgPanel"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left
            };
            Image ashIcon = new Image {
                Width = 20, Height = 20, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            ashIconFrame.Child = ashIcon;
            ashPreview.Children.Add(ashIconFrame);
            TextBlock ashName = new TextBlock {
                Text = "None", Foreground = GetResourceBrush("TextMain"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(ashName, 1);
            ashPreview.Children.Add(ashName);
            Grid.SetColumn(ashPreview, 1);
            ashRow.Children.Add(ashPreview);
            Button pickAshButton = new Button {
                Content = "Pick...", Style = (Style) FindResource("ActionButton"), MinWidth = 88, Height = 32, Padding = new Thickness(12, 2, 12, 2), IsEnabled = false, Focusable = false, FocusVisualStyle = null
            };
            Grid.SetColumn(pickAshButton, 2);
            ashRow.Children.Add(pickAshButton);
            Button clearAshButton = new Button {
                Content = "Clear", Style = (Style) FindResource("ActionButton"), MinWidth = 74, Height = 32, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(7, 0, 0, 0), IsEnabled = false, Focusable = false, FocusVisualStyle = null
            };
            Grid.SetColumn(clearAshButton, 3);
            ashRow.Children.Add(clearAshButton);
            Grid.SetRow(ashRow, 2);
            Grid.SetColumnSpan(ashRow, 4);
            footerGrid.Children.Add(ashRow);
            Button addButton = new Button {
                Content = "Add Item", Style = (Style) FindResource("PrimaryButton"), IsEnabled = false, Width = 150, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 11, 0, 0), Height = 34, FontSize = 12.5, Focusable = false, FocusVisualStyle = null
            };
            Grid.SetRow(addButton, 3);
            Grid.SetColumnSpan(addButton, 4);
            footerGrid.Children.Add(addButton);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            InventoryCatalogItem? selectedItem = null;
            InventoryCatalogItem? selectedAshOfWar = null;
            List<InventoryCatalogItem> catalog = new List<InventoryCatalogItem>();
            async void RefreshAshPreview() {
                ashIcon.Source = null;
                if (selectedAshOfWar == null) {
                    ashName.Text = "None";
                    clearAshButton.IsEnabled = false;
                    return;
                }
                ashName.Text = selectedAshOfWar.Name;
                clearAshButton.IsEnabled = true;
                try {
                    await LoadInventoryCatalogIconAsync(selectedAshOfWar, ashIcon);
                } catch {
                    ashIcon.Source = null;
                }
            }
            void ConfigureForSelection() {
                if (selectedItem == null) {
                    selectedText.Text = "No item selected";
                    addButton.IsEnabled = false;
                    upgradeBox.IsEnabled = false;
                    affinityBox.IsEnabled = false;
                    pickAshButton.IsEnabled = false;
                    selectedAshOfWar = null;
                    RefreshAshPreview();
                    return;
                }
                selectedText.Text = selectedItem.Name + "  •  " + selectedItem.Category + (selectedItem.Category == "Weapons"?$"  •  Max +{selectedItem.MaxUpgrade}" : "");
                selectedText.Foreground = selectedItem.Category == "Weapons"? GetResourceBrush("AccentBright") : GetResourceBrush("TextSoft");
                addButton.IsEnabled = true;
                bool weapon = selectedItem.Category == "Weapons";
                upgradeBox.IsEnabled = weapon;
                affinityBox.IsEnabled = weapon && selectedItem.SupportsAffinity;
                pickAshButton.IsEnabled = weapon && selectedItem.SupportsAshOfWar;
                if (weapon) {
                    int max = Math.Max(0, selectedItem.MaxUpgrade);
                    upgradeBox.ItemsSource = Enumerable.Range(0, max + 1).Select(x => "+" + x).ToList();
                    upgradeBox.SelectedIndex = 0;
                    if (!selectedItem.SupportsAffinity) affinityBox.SelectedIndex = 0;
                } else {
                    upgradeBox.ItemsSource = new[] {
                        "N/A"
                    };
                    upgradeBox.SelectedIndex = 0;
                    affinityBox.SelectedIndex = 0;
                    selectedAshOfWar = null;
                    RefreshAshPreview();
                }
            }
            async System.Threading.Tasks.Task RenderCardsAsync() {
                cards.Children.Clear();
                string category = categoryBox.SelectedItem?.ToString() ?? "Weapons";
                string q = searchBox.Text.Trim();
                List<InventoryCatalogItem> visible = catalog.Where(x => x.Category == category).Where(IsUserFacingInventoryAddItem).Where(x => string.IsNullOrWhiteSpace(q) || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(350).ToList();
                foreach (InventoryCatalogItem item in visible) {
                    Border card = new Border {
                        Width = 124, Height = 136, Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(4), Padding = new Thickness(6), Cursor = Cursors.Hand, Tag = item
                    };
                    StackPanel cs = new StackPanel();
                    Image img = new Image {
                        Width = 64, Height = 64, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, SnapsToDevicePixels = true, UseLayoutRounding = true
                    };
                    cs.Children.Add(img);
                    cs.Children.Add(new TextBlock {
                        Text = item.Name, Foreground = GetResourceBrush("TextMain"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 11.5, MaxHeight = 44, Margin = new Thickness(0, 5, 0, 0)
                    });
                    card.Child = cs;
                    card.MouseEnter += (_, _) => {
                        if (!ReferenceEquals(card.Tag, selectedItem)) card.Background = GetResourceBrush("DropHover");
                    };
                    card.MouseLeave += (_, _) => {
                        if (!ReferenceEquals(card.Tag, selectedItem)) card.Background = GetResourceBrush("BgPanel2");
                    };
                    card.MouseLeftButtonUp += (_, _) => {
                        selectedItem = item;
                        selectedAshOfWar = null;
                        RefreshAshPreview();
                        ConfigureForSelection();
                        foreach (Border sibling in cards.Children.OfType<Border>()) {
                            bool isSelected = ReferenceEquals(sibling.Tag, selectedItem);
                            sibling.Background = isSelected? GetResourceBrush("DropSelected") : GetResourceBrush("BgPanel2");
                            sibling.BorderBrush = isSelected? GetResourceBrush("AccentBright") : GetResourceBrush("BorderSoft");
                            sibling.BorderThickness = isSelected? new Thickness(2) : new Thickness(1);
                        }
                    };
                    cards.Children.Add(card);
                    _ = LoadInventoryCatalogIconAsync(item, img);
                }
                if (visible.Count == 0) {
                    cards.Children.Add(new TextBlock {
                        Text = "No matching items.", Foreground = GetResourceBrush("TextMuted"), Margin = new Thickness(8)
                    });
                }
                await System.Threading.Tasks.Task.CompletedTask;
            }
            searchBox.TextChanged += async(_, _) => await RenderCardsAsync();
            categoryBox.SelectionChanged += async(_, _) => {
                selectedItem = null;
                selectedAshOfWar = null;
                ConfigureForSelection();
                await RenderCardsAsync();
            };
            pickAshButton.Click += (_, _) => {
                if (selectedItem == null || selectedItem.Category != "Weapons" ||!selectedItem.SupportsAshOfWar) return;
                AshOfWarSelection choice = ShowInventoryAshOfWarPickerDialog(window, selectedItem, catalog, selectedAshOfWar);
                if (!choice.Accepted) return;
                selectedAshOfWar = choice.AshOfWar;
                RefreshAshPreview();
            };
            clearAshButton.Click += (_, _) => {
                selectedAshOfWar = null;
                RefreshAshPreview();
            };
            addButton.Click += (_, _) => {
                if (selectedItem == null) return;
                if (!uint.TryParse(quantityBox.Text.Trim(), out uint quantity) || quantity<1 || quantity> 9999) {
                    ShowToast("Quantity must be from 1 to 9,999", ToastKind.Warning);
                    return;
                }
                int upgrade = 0;
                if (selectedItem.Category == "Weapons") {
                    string upgradeText = upgradeBox.SelectedItem?.ToString()?.TrimStart('+') ?? "0";
                    int.TryParse(upgradeText, out upgrade);
                }
                if (selectedItem.Category == "Weapons" &&!ValidateInventoryWeaponUpgrade(selectedItem.Name, upgrade, selectedItem.MaxUpgrade)) return;
                int affinity = selectedItem.SupportsAffinity? Math.Max(0, affinityBox.SelectedIndex) : 0;
                string location = locationBox.SelectedIndex == 1? "Storage" : "Held";
                byte[] rollbackBytes = File.ReadAllBytes(loadedSavePath);
                bool itemWasWritten = false;
                try {
                    uint addedHandle = AddInventoryCatalogItem(loadedSavePath, slotIndex, selectedItem, quantity, upgrade, affinity, location);
                    itemWasWritten = true;
                    if (selectedAshOfWar != null && selectedItem.Category == "Weapons") {
                        InventorySnapshot refreshed = ReadInventorySnapshot(loadedSavePath, slotIndex);
                        InventoryEntryInfo? addedEntry = refreshed.Items.FirstOrDefault(x => x.Location == location && x.Handle == addedHandle && x.Category == "Weapons");
                        if (addedEntry == null) throw new InvalidOperationException("The newly added weapon could not be located for Ash of War assignment.");
                        UpdateInventoryWeaponAshOfWar(loadedSavePath, slotIndex, addedEntry, selectedAshOfWar);
                    }
                    ShowToast($"Added {selectedItem.Name} • {location}", ToastKind.Success);
                    selectedItem = null;
                    selectedAshOfWar = null;
                    ConfigureForSelection();
                    RefreshAshPreview();
                } catch (Exception ex) {
                    if (itemWasWritten && selectedAshOfWar != null) {
                        try {
                            WriteSaveAtomically(loadedSavePath, rollbackBytes);
                        } catch {
                        }
                    }
                    ShowToast("Could not add item • " + ex.Message, ToastKind.Warning);
                }
            };
            ConfigureForSelection();
            window.Content = root;
            try {
                catalog = await LoadInventoryCatalogAsync();
                await RenderCardsAsync();
            } catch (Exception ex) {
                cards.Children.Clear();
                cards.Children.Add(new TextBlock {
                    Text = "Item catalog unavailable. " + ex.Message, Foreground = GetResourceBrush("TextMuted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8)
                });
                ShowToast("Inventory catalog unavailable", ToastKind.Warning);
            }
            window.ShowDialog();
        }
        private async System.Threading.Tasks.Task OpenVisualInventoryWindowAsync(int slotIndex, string initialCategory) {
            Window window = CreateInventoryToolWindow("Edit Inventory", 980, 720);
            Grid root = new Grid {
                Margin = new Thickness(14)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            Grid toolbar = new Grid {
                Margin = new Thickness(0, 0, 0, 8)
            };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(0.82, GridUnitType.Star)
            });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1.02, GridUnitType.Star)
            });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1.00, GridUnitType.Star)
            });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(0.74, GridUnitType.Star)
            });
            toolbar.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            toolbar.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            ComboBox locationBox = CreateInventoryCombo(new[] {
                "Inventory", "Storage"
            });
            locationBox.SelectedIndex = 0;
            locationBox.Height = 34;
            locationBox.Margin = new Thickness(0, 0, 7, 0);
            ComboBox categoryBox = CreateInventoryCombo(new[] {
                "All", "Weapons", "Armor", "Talismans", "Consumables", "Sorceries", "Incantations", "Spirit Ashes", "Ashes of War", "Key Items"
            });
            string normalized = NormalizeInventoryToolCategory(initialCategory);
            categoryBox.SelectedItem = normalized;
            if (categoryBox.SelectedIndex<0) categoryBox.SelectedIndex = 0;
            categoryBox.Height = 34;
            categoryBox.Margin = new Thickness(0, 0, 7, 0);
            ComboBox sortBox = CreateInventoryCombo(new[] {
                "A-Z", "Z-A", "Quantity", "Category"
            });
            sortBox.SelectedIndex = 0;
            sortBox.Height = 34;
            sortBox.Margin = new Thickness(0, 0, 7, 0);
            Button closeButton = new Button {
                Content = "Close", Style = (Style) FindResource("ActionButton"), Height = 34, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Focusable = false, FocusVisualStyle = null
            };
            closeButton.Click += (_, _) => window.Close();
            TextBox filterBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ApplyInventorySearchBoxTheme(filterBox);
            Grid.SetColumn(locationBox, 0);
            Grid.SetRow(locationBox, 0);
            toolbar.Children.Add(locationBox);
            Grid.SetColumn(categoryBox, 1);
            Grid.SetRow(categoryBox, 0);
            toolbar.Children.Add(categoryBox);
            Grid.SetColumn(sortBox, 2);
            Grid.SetRow(sortBox, 0);
            toolbar.Children.Add(sortBox);
            Grid.SetColumn(closeButton, 3);
            Grid.SetRow(closeButton, 0);
            toolbar.Children.Add(closeButton);
            Grid.SetColumn(filterBox, 0);
            Grid.SetColumnSpan(filterBox, 4);
            Grid.SetRow(filterBox, 1);
            toolbar.Children.Add(filterBox);
            root.Children.Add(toolbar);
            ScrollViewer scroll = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            WrapPanel cards = new WrapPanel();
            scroll.Content = cards;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);
            Border footer = CreateCard();
            footer.Margin = new Thickness(0, 10, 0, 0);
            StackPanel footerStack = new StackPanel();
            TextBlock selectedText = CreateFieldLabel("No item selected");
            footerStack.Children.Add(selectedText);
            StackPanel actions = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0)
            };
            Button removeButton = CreateInventoryActionButton("Remove", false);
            Button quantityButton = CreateInventoryActionButton("Set Quantity", false);
            Button upgradeButton = CreateInventoryActionButton("Set Upgrade", false);
            Button affinityButton = CreateInventoryActionButton("Set Affinity", false);
            Button ashButton = CreateInventoryActionButton("Set AoW", false);
            actions.Children.Add(removeButton);
            actions.Children.Add(quantityButton);
            actions.Children.Add(upgradeButton);
            actions.Children.Add(affinityButton);
            actions.Children.Add(ashButton);
            footerStack.Children.Add(actions);
            footer.Child = footerStack;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            InventoryEntryInfo? selected = null;
            InventorySnapshot snapshot = ReadInventorySnapshot(loadedSavePath, slotIndex);
            List<InventoryCatalogItem> catalog;
            try {
                catalog = await LoadInventoryCatalogAsync();
            } catch {
                catalog = new List<InventoryCatalogItem>();
            }
            ApplyCatalogNames(snapshot, catalog);
            void UpdateActions() {
                InventoryEntryInfo? current = selected;
                if (current == null) {
                    removeButton.IsEnabled = false;
                    quantityButton.IsEnabled = false;
                    upgradeButton.IsEnabled = false;
                    affinityButton.IsEnabled = false;
                    ashButton.IsEnabled = false;
                    selectedText.Text = "No item selected";
                    return;
                }
                removeButton.IsEnabled = true;
                quantityButton.IsEnabled = true;
                bool weapon = current.Category == "Weapons";
                upgradeButton.IsEnabled = weapon;
                affinityButton.IsEnabled = weapon && current.SupportsAffinity;
                ashButton.IsEnabled = weapon && current.SupportsAshOfWar;
                string displayLocation = current.Location == "Held"? "Inventory" : current.Location;
                selectedText.Text = $"{GetInventoryEntryDisplayName(current)}  •  Qty {current.Quantity:N0}  •  {displayLocation}" + (weapon?$"  •  +{current.Upgrade}  •  {GetInventoryAffinityNames()[Math.Clamp(current.Affinity, 0, 12)]}  •  AoW: {current.AshOfWarName}" : "");
            }
            async System.Threading.Tasks.Task RenderAsync() {
                cards.Children.Clear();
                selected = null;
                UpdateActions();
                string loc = locationBox.SelectedItem?.ToString() ?? "Inventory";
                string cat = categoryBox.SelectedItem?.ToString() ?? "All";
                string q = filterBox.Text.Trim();
                string internalLoc = loc == "Inventory"? "Held" : loc;
                IEnumerable<InventoryEntryInfo> query = snapshot.Items.Where(x => x.Location == internalLoc);
                if (cat != "All") query = query.Where(x => x.Category == cat);
                if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
                query = sortBox.SelectedIndex switch {
                    1 => query.OrderByDescending(x => x.Name), 2 => query.OrderByDescending(x => x.Quantity).ThenBy(x => x.Name), 3 => query.OrderBy(x => x.Category).ThenBy(x => x.Name), _ => query.OrderBy(x => x.Name)
                };
                foreach (InventoryEntryInfo entry in query.Take(900)) {
                    Border card = new Border {
                        Width = 128, Height = 146, Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(7), Margin = new Thickness(4), Cursor = Cursors.Hand, Tag = entry
                    };
                    StackPanel cs = new StackPanel();
                    Image img = new Image {
                        Width = 64, Height = 64, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, SnapsToDevicePixels = true, UseLayoutRounding = true
                    };
                    cs.Children.Add(img);
                    cs.Children.Add(new TextBlock {
                        Text = GetInventoryEntryDisplayName(entry), Foreground = GetResourceBrush("TextMain"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxHeight = 39, Margin = new Thickness(0, 4, 0, 0)
                    });
                    cs.Children.Add(new TextBlock {
                        Text = "Qty " + entry.Quantity + (entry.Category == "Weapons"? "  •  +" + entry.Upgrade : ""), Foreground = GetResourceBrush("TextMuted"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0)
                    });
                    card.Child = cs;
                    card.MouseLeftButtonUp += (_, _) => {
                        selected = entry;
                        UpdateActions();
                        foreach (Border sibling in cards.Children.OfType<Border>()) {
                            bool on = ReferenceEquals(sibling.Tag, selected);
                            sibling.BorderBrush = on? GetResourceBrush("AccentBright") : GetResourceBrush("BorderSoft");
                            sibling.BorderThickness = on? new Thickness(2) : new Thickness(1);
                        }
                    };
                    cards.Children.Add(card);
                    InventoryCatalogItem? catItem = FindCatalogItemForEntry(entry, catalog);
                    if (catItem != null) _ = LoadInventoryCatalogIconAsync(catItem, img);
                }
                await System.Threading.Tasks.Task.CompletedTask;
            }
            async System.Threading.Tasks.Task ReloadAsync() {
                snapshot = ReadInventorySnapshot(loadedSavePath, slotIndex);
                ApplyCatalogNames(snapshot, catalog);
                await RenderAsync();
            }
            locationBox.SelectionChanged += async(_, _) => await RenderAsync();
            categoryBox.SelectionChanged += async(_, _) => await RenderAsync();
            sortBox.SelectionChanged += async(_, _) => await RenderAsync();
            filterBox.TextChanged += async(_, _) => await RenderAsync();
            removeButton.Click += async(_, _) => {
                if (selected == null) return;
                if (MessageBox.Show(window, $"Remove {selected.Name} from {selected.Location}?", "Remove Item", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try {
                    RemoveInventoryEntry(loadedSavePath, slotIndex, selected);
                    ShowToast("Item removed", ToastKind.Success);
                    await ReloadAsync();
                } catch (Exception ex) {
                    ShowToast("Could not remove item • " + ex.Message, ToastKind.Warning);
                }
            };
            quantityButton.Click += async(_, _) => {
                if (selected == null) return;
                string?value = ShowInventoryValueDialog(window, "Set Quantity", "Quantity (1–9,999)", selected.Quantity.ToString(), null);
                if (value == null) return;
                if (!uint.TryParse(value, out uint qty) || qty<1 || qty> 9999) {
                    ShowToast("Quantity must be from 1 to 9,999", ToastKind.Warning);
                    return;
                }
                try {
                    UpdateInventoryQuantity(loadedSavePath, slotIndex, selected, qty);
                    ShowToast("Quantity updated", ToastKind.Success);
                    await ReloadAsync();
                } catch (Exception ex) {
                    ShowToast("Could not update quantity • " + ex.Message, ToastKind.Warning);
                }
            };
            upgradeButton.Click += async(_, _) => {
                if (selected == null || selected.Category != "Weapons") return;
                int max = Math.Max(0, selected.MaxUpgrade);
                string?value = ShowInventoryValueDialog(window, "Set Upgrade", $"Upgrade level (0–{max})", selected.Upgrade.ToString(), null);
                if (value == null) return;
                if (!int.TryParse(value.TrimStart('+'), out int upgrade)) return;
                if (!ValidateInventoryWeaponUpgrade(selected.Name, upgrade, selected.MaxUpgrade)) return;
                try {
                    UpdateInventoryWeaponVariant(loadedSavePath, slotIndex, selected, upgrade, selected.Affinity);
                    ShowToast("Weapon upgrade updated", ToastKind.Success);
                    await ReloadAsync();
                } catch (Exception ex) {
                    ShowToast("Could not update upgrade • " + ex.Message, ToastKind.Warning);
                }
            };
            affinityButton.Click += async(_, _) => {
                if (selected == null || selected.Category != "Weapons" ||!selected.SupportsAffinity) return;
                string?value = ShowInventoryAffinityDialog(window, GetInventoryAffinityNames()[Math.Clamp(selected.Affinity, 0, 12)]);
                if (value == null) return;
                int affinity = Array.IndexOf(GetInventoryAffinityNames(), value);
                if (affinity<0) affinity = 0;
                try {
                    UpdateInventoryWeaponVariant(loadedSavePath, slotIndex, selected, selected.Upgrade, affinity);
                    ShowToast("Weapon affinity updated", ToastKind.Success);
                    await ReloadAsync();
                } catch (Exception ex) {
                    ShowToast("Could not update affinity • " + ex.Message, ToastKind.Warning);
                }
            };
            ashButton.Click += async(_, _) => {
                if (selected == null || selected.Category != "Weapons" ||!selected.SupportsAshOfWar) return;
                InventoryCatalogItem? weaponMeta = FindCatalogItemForEntry(selected, catalog);
                AshOfWarSelection choice = ShowInventoryAshOfWarDialog(window, selected, weaponMeta, catalog);
                if (!choice.Accepted) return;
                try {
                    UpdateInventoryWeaponAshOfWar(loadedSavePath, slotIndex, selected, choice.AshOfWar);
                    ShowToast(choice.AshOfWar == null? "Ash of War cleared" : "Ash of War updated", ToastKind.Success);
                    await ReloadAsync();
                } catch (Exception ex) {
                    ShowToast("Could not update Ash of War • " + ex.Message, ToastKind.Warning);
                }
            };
            window.Content = root;
            await RenderAsync();
            window.Show();
        }
        private AshOfWarSelection ShowInventoryAshOfWarDialog(Window owner, InventoryEntryInfo entry, InventoryCatalogItem? weaponMeta, List<InventoryCatalogItem> catalog) {
            InventoryCatalogItem? currentAsh = entry.AshOfWarParamId == 0u? null : catalog.FirstOrDefault(x => x.Category == "Ashes of War" && x.ParamId == entry.AshOfWarParamId);
            return ShowInventoryAshOfWarDialogCore(owner, GetInventoryEntryDisplayName(entry), currentAsh, weaponMeta, catalog);
        }
        private AshOfWarSelection ShowInventoryAshOfWarPickerDialog(Window owner, InventoryCatalogItem weaponMeta, List<InventoryCatalogItem> catalog, InventoryCatalogItem? currentAsh) {
            return ShowInventoryAshOfWarDialogCore(owner, weaponMeta.Name, currentAsh, weaponMeta, catalog);
        }
        private AshOfWarSelection ShowInventoryAshOfWarDialogCore(Window owner, string weaponName, InventoryCatalogItem? currentAsh, InventoryCatalogItem? weaponMeta, List<InventoryCatalogItem> catalog) {
            AshOfWarSelection result = new AshOfWarSelection();
            Window dialog = CreateInventoryToolWindow("Set Ash of War", 500, 555);
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ResizeMode = ResizeMode.NoResize;
            Grid root = new Grid {
                Margin = new Thickness(14)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            TextBlock info = new TextBlock {
                Text = weaponName + "  •  Current: " + (currentAsh?.Name ?? "None"), Foreground = GetResourceBrush("TextMain"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), FontSize = 12
            };
            root.Children.Add(info);
            TextBox search = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search compatible Ashes of War"
            };
            ApplyInventorySearchBoxTheme(search);
            Grid.SetRow(search, 1);
            root.Children.Add(search);
            ListBox list = new ListBox {
                Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), Foreground = GetResourceBrush("TextMain"), Margin = new Thickness(0, 9, 0, 9), HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            list.ItemContainerStyle = CreateInventoryPopupListItemStyle();
            Grid.SetRow(list, 2);
            root.Children.Add(list);
            List<InventoryCatalogItem> compatible = new List<InventoryCatalogItem>();
            if (weaponMeta != null && weaponMeta.SupportsAshOfWar &&!string.IsNullOrWhiteSpace(weaponMeta.WeaponTypeColumn)) {
                string weaponType = weaponMeta.WeaponTypeColumn.Trim();
                compatible = catalog.Where(x => x.Category == "Ashes of War").Where(x =>!string.IsNullOrWhiteSpace(x.CompatibleWeaponTypes)).Where(x => x.CompatibleWeaponTypes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(t => t.Equals(weaponType, StringComparison.OrdinalIgnoreCase))).GroupBy(x => x.ParamId).Select(g => g.First()).OrderBy(x => x.Name).ToList();
            }
            void Render() {
                string q = search.Text.Trim();
                list.Items.Clear();
                List<InventoryCatalogItem> shown = compatible.Where(x => string.IsNullOrWhiteSpace(q) || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
                ListBoxItem? currentRow = null;
                foreach (InventoryCatalogItem ash in shown) {
                    Grid rowContent = new Grid {
                        Margin = new Thickness(2, 0, 2, 0)
                    };
                    rowContent.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(30)
                    });
                    rowContent.ColumnDefinitions.Add(new ColumnDefinition {
                        Width = new GridLength(1, GridUnitType.Star)
                    });
                    Border iconFrame = new Border {
                        Width = 24, Height = 24, Background = GetResourceBrush("BgPanel"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
                    };
                    Image icon = new Image {
                        Width = 20, Height = 20, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    iconFrame.Child = icon;
                    rowContent.Children.Add(iconFrame);
                    TextBlock text = new TextBlock {
                        Text = ash.Name, Foreground = GetResourceBrush("TextMain"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(3, 0, 0, 0), FontSize = 12
                    };
                    Grid.SetColumn(text, 1);
                    rowContent.Children.Add(text);
                    ListBoxItem row = new ListBoxItem {
                        Content = rowContent, Tag = ash, ToolTip = ash.Name
                    };
                    list.Items.Add(row);
                    _ = LoadInventoryCatalogIconAsync(ash, icon);
                    if (currentAsh != null && ash.ParamId == currentAsh.ParamId) currentRow = row;
                }
                if (currentRow != null) {
                    currentRow.IsSelected = true;
                    currentRow.BringIntoView();
                }
                if (shown.Count == 0) {
                    string message = weaponMeta == null || string.IsNullOrWhiteSpace(weaponMeta.WeaponTypeColumn)? "Weapon compatibility metadata is unavailable; no Ash of War is offered for safety." : "No compatible Ash of War matches this weapon/search.";
                    list.Items.Add(new ListBoxItem {
                        Content = message, IsEnabled = false, Foreground = GetResourceBrush("TextMuted")
                    });
                }
            }
            search.TextChanged += (_, _) => Render();
            Grid buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Button clear = CreateInventoryActionButton("Clear AoW", true);
            Button apply = CreateInventoryActionButton("Set AoW", true);
            Button close = CreateInventoryActionButton("Close", true);
            clear.Width = double.NaN;
            apply.Width = double.NaN;
            close.Width = double.NaN;
            clear.HorizontalAlignment = HorizontalAlignment.Stretch;
            apply.HorizontalAlignment = HorizontalAlignment.Stretch;
            close.HorizontalAlignment = HorizontalAlignment.Stretch;
            clear.Margin = new Thickness(0, 0, 5, 0);
            apply.Margin = new Thickness(5, 0, 5, 0);
            close.Margin = new Thickness(5, 0, 0, 0);
            Grid.SetColumn(clear, 0);
            Grid.SetColumn(apply, 1);
            Grid.SetColumn(close, 2);
            buttons.Children.Add(clear);
            buttons.Children.Add(apply);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);
            clear.Click += (_, _) => {
                result.Accepted = true;
                result.AshOfWar = null;
                dialog.DialogResult = true;
            };
            apply.Click += (_, _) => {
                if ((list.SelectedItem as ListBoxItem)?.Tag is not InventoryCatalogItem ash) return;
                result.Accepted = true;
                result.AshOfWar = ash;
                dialog.DialogResult = true;
            };
            close.Click += (_, _) => dialog.Close();
            list.MouseDoubleClick += (_, _) => {
                if ((list.SelectedItem as ListBoxItem)?.Tag is not InventoryCatalogItem ash) return;
                result.Accepted = true;
                result.AshOfWar = ash;
                dialog.DialogResult = true;
            };
            dialog.Content = root;
            Render();
            dialog.ShowDialog();
            return result;
        }
        private Window CreateInventoryToolWindow(string title, double width, double height) {
            Window window = new Window {
                Title = title, Width = width, Height = height, MinWidth = Math.Min(width, 760), MinHeight = Math.Min(height, 560), Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = GetResourceBrush("BgMain"), Foreground = GetResourceBrush("TextMain"), FontFamily = FontFamily, ResizeMode = ResizeMode.CanResize
            };
            foreach (object key in Resources.Keys) window.Resources[key] = Resources[key];
            return window;
        }
        private ComboBox CreateInventoryCombo(IEnumerable<string> values) {
            ComboBox combo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), MinWidth = 96, Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 8, 2), VerticalContentAlignment = VerticalAlignment.Center, ItemsSource = values.ToList()
            };
            return combo;
        }
        private sealed class InventoryAffinityOption {
            public string Name {
                get;
                set;
            }
            = "";
            public ImageSource? Icon {
                get;
                set;
            }
        }
        private const int InventoryAffinityIconCellSize = 23;
        private const string InventoryAffinityIconSpriteBase64 = "iVBORw0KGgoAAAANSUhEUgAAABcAAAErCAYAAAA15RjCAAA8A0lEQVR42t29aZBlyXme92Tm2e9+a6+utffpmelZMRisI5KgQIlkyA5CjBBlK6yQHSHSNuUISRE0BVGWQEH+4wjJP/zLMm1ZlkSQJgUKFIlFBDDEDDAYYPbprt67eqvlVtXd7z1bZvrHud1VNQJoSmHJId2Iiujo6s7KypP5fW++3/u9R3Q6Hcu/o4/z8A/tdpvt7W2MMVj7g3+eEALgj/y+Uor5+Xnq9XoxeKfT4W/+zV/m2o0rVCsV8jzHWovrOAwGQ6SQuK6D7weAwJgc13XIsxzHdcnyHItAYBmOhqytn+bvfu7vIQFarRa3N28zM91kOOxhTI7RGf1uByUtjpLkeQpYrDH0ej3SNMEKQzweY3ROmsUMRwOaUw2uXnufu3fvFjPXWmOtodXaYW+3jeMolHLwfZ84STHGIqVkNByTpCmVSpkf+ZEf5Rsv/yFzcw08x+P3vvYVHMdBa4MSCostZm6tJTcZjcYUnufh+wGVSoUgCCiXy5RKJUqlEvVGgygKefrpZ8CCAJ577jl2dncxBqrVGghBmiYoKYuZW2upRBGu4+K6DmEQ4Hkeruviui6OUqRZilIOc3NzrK+v8/I3v8nq6gqtVotr164yPzuD47ooIfD9kCzLi5kDZGlGv9fHcYrlUEqilMQYgzYGx3HI85z19ZNYY5BKMTs7y/uX3scYTaNRw5EgLOhcYK0pBhdCMI7H+L6PFBIhBFJIBv0BaZoipSDLcnSe89RTF+n3+ywtLSGAK1euUK3X8P0AC/hBQKZTQBzOPPAikjglz3OSNAUhcD0XIcBxHMAyMztDGIbcvHmTSqXMxuUN4jih0WgQx2PyPCdLEyLfPX6Icm1AG4SUKCmx1qC1AQRaa7I044UXXuD27dsEk2fy3nvvUSmXUUoVu0QpsixBW4uAw5ljDbVqFSkFUkmklHhu8VAHgyHNqSYn109ycNBmbm6O25ubjMYjGo3mwwGQUhGF0eQ4HRk8s4bcZEgE1liMtVgsSEiSmKcuPkWv36PX6+G5Lu+9+x61WrNYawtCWBCGLC/Wm6OD+67HYDggNxpjLNZCri06tzSaUzz22GN8//vfx3Udbt++Ta/fp1FvFA9aa6y1GK1J0xRHOscHFxJcxwPpoFwPpTyiqEyew7NPPYO1loNOl1qtzqXLGzQaDcqVEgiQSqKNwRhLFEVoaw4Hl1Jico2SLlFYplKtEQQhvu/jeS5nzp5l4/IVFucX2d7a5WD/gFq9jrYGbTXaGrwgwAtKKNcnzhOsnQxujKFcKoG1hGFIrVplfmGeMAw5e/YsYbnCfrtDpVRh49IlGo0GgR/iugHT03MIq8gzgaNC8gwqpWqxhYtDBEhJWIpYWlrj1KlTpFnKaDTiicefYGPjCs3mFLu7u6yunaRcrVGulImiiDhOODjYw1hI4oQ0GbLf3jvc50JI+v0BQRjguh55pjHaUilX2dvbZ6/VIipFGCE4sbyCsXYSJgLCIJxsR8He3g5Xrr7PoN8/nLm1FuW6ZFnGzVtXGccDlJTFD8pzpBTs7BRbcL89xFrIsgwpi/1gtCWOxxy090iSMX5QbE/n4QNNRkNmZqYZxkPu3buF67lIqbDW4Hk+SRzjeR7DwZDxaIxUCp1rlCOLLCQlOtfUamUebO0iH4bcmZkZVlfXuHrtKuVKmfFwTK41QRAwGo1RjkRMlu9hCszzHKUgKpWIxylCSBxHcu9eh5MnT7OwsIB4mP339vZotXbJc/1DE/Af9RFCFMs7SdBTU1OHgauIfAIp5b82+Aez/sOBftjgSqnD3dLtdvnsr3yWa1cuU62UyXONEBblFLtIKYnruHiuh5AKa/Ukfigc5ZDrZJJYcnq9ESdPneVv/+2/c5j9b926xszsFKPhaBJuNb1uD0dKXOWQpQlCFgGq3+8DFm0SRqMeSRIzHo/p94ZMT09x/eol7t9/UMw8z3MElt3dbQ5aByjXwZEKz/dJkpRJ2GM4GpOmMfV6gyeefIJf/43fIAxCpqemuX/3PtpYEBahJNbYw31uraHZbNLv9HFchygqoZQq4o4xSCnwXI/BsMczzzzF/t4eaZrRaDQZjUZYa6nVqghrSUbjIic8fCCVchnPdXE9l1KphOe6BIFPFEU0GnXCMMLzPWZm5lhcXOKtt95iqtlEWEs8HlOr1/BcB4QiCEtYqw8TdDyK6XV7KOXgeR5SKYSQYC1aa5SU5Lnm9OkzXL16nf29AyQCJYq02GjUcL1i82mTo82ReD6KR/i+h1KqAJRSMhwOSNIUKSW5zhFCUC6XePXVV/GDAGMMw+EQ3/fwPB9rLL7vkeUZ2COIKwwj4iQlzzPSNAUJnucCReIVCJrNJvfu3afVahXZy/eRSlGp1RknManO0bkl8ELsw8GFEGQ6L/KfLY65NQZj8mLL6Zxca06fPsW777xDFIZ4nkeaJBPooQr4phzSZIzO0gL7PDp5FiqVCo7joJRESoXn+TiOy2g0pl6v0e12ebC1VWBvRyGVZGV5BddRSAGOkkSlAOWo4zlU6/zYkRZSYoXCCkGWZSwuLvLWW28RRSUc10dJh7/8l/8y5x87x3A4xBiN1hl5rov9fnRwx3Xo9fvkOscYQ5IkjEYj8lzTbE4TBhGbd+5RrdYY9Pt89GMfJ8/glW99l0FvzGPnHqdZn2Y0ipHiAzMXFnzHw3V8hFDUa7UibCI4deo0rdYeSkh63U6xkwZDvvjFL9LrDqjVmly6dJWdnT2isISZrMCjZCEMeK5P6kpq9TrPPfcU3/3u67jKpVqt8tWvfJlqpcKfeOmTlMsVBoMhuzu71Go1tDFcu3kDIaBWr5JpfThzIQSVSpUsz5luNvjpn/wJvvvaG3zly39Ar9fn3t17dDsDfuJP/xTC8dm4doNvfutVpONy0G5z/cZ1pqan+ORLLxGEAVEYHW5FrTVCSCrVKj/xEz/O5q1N3n7nPcKwzPMf+hCtVou//w/+PtPTM3zpX/wuGxvXyHPLcDgmzzRKuVQrNW7euEUQhAhRTPjRsnR6PUpRxNe++jWyLGdhboaLF5/A5JbTZ85w795dNi5fZm52liRJ6ff7PP7YYxhr2LiyQX/Q4/69+wSBIhmND9fcGIPnuozHYzw/YHpulrnFBaqVCv3+kP39PW7fvkmlUmFtfRlrod/vUy4HbG1tMb8wjcWSZdPoPCWKwuMPdBwnTE01GIwHZLsJruOyt7eLNRrX9zBoBqM+w+GgmITn0X6wj+97uNJHG8Py8gKB7/Pgwc7x7L+0tMy1a1coVUvEoxFpmhGFAUlSgPkioEGa5mhdhAWEpKQtcZxM7lCKVuuAUyfPsri4eJj99/f32d3dmeRPMUkg/+YoQErJ/MIC01NTh4P/OyUWOp0OW1tbGGP+2P/ZWosQ4hjUcJzirnqMWPiVX/ksV69tUC5HxdJAkTMHAwCU4+B7HkpJtDa4jvNowDTPEWpyvRwMWFs/dZxYuHnrGlPTdYbDPlqnGJPT6RwgJbiuIs8ShLBobRiNRozHI8bjMUJaoqi43fV6HRqNKjeuX/sAsYBld3uXg/0WnuehpIPjuCTJ5JYgBcPhCGsMCLDG4vk+T158gsUTJxiMRvzPf/8fcOGx8yhHYe0RaKF1TnOqyaDfLciFIMRRLlKKR6yFoxTGWrIsJU1TnnnmGaIo4vTp03zntddwHAfHcRiNhyilDh9oNSrj+z6u6xEERRpznENyIU0SlFJ4nkeSJriux9mzZ9jb26NWq3Hr1i2CIMBxXAI/Quv0yA06zeimKa7r4/thkQOlmMDlDKlkkQIdBSmsra0Rx0lxWITk5s2b1BsNtLboPEdre5gsCmLBexTRlJIMh0PSNEVJRZblGGsZDAYIITh18iSDwYCPfOSj3Lp1k8FgQFQKcT2JzvPjmcj1fNI0Jc9ykiRBCHEILRxF4PtkWYq1lpMnT2KlQCjJ7Pwcr7/+PZSQYAxZNsYPQ+xxYkEjTAE4C3yo0TpHOQ55lgGCMIxQUvLM08+wee8uL3z4BUajEe+99x71Rh0BaG3Ic4M4SokYa6hVaxN+6yG08AoskmVoY8iyjCeeLLae67o899zzvPLqd+j1+3zypZf41Kc+xdzsPFYbsEeWxWpLrnNAUBw8AVYWeHFyFi5cuMCnP/1p7ty5y8n105RKFf75F/85L3z4RT7zmZ/l9Nnz1Ot1kgmWf7QsfugzGPQfLYcxDghZ3EerZZ5//kP89E//FLdv3WJ/f5+f+cxn+MY3vkGeaX7h53+Bb37zG/zOF38Ha1JKpfIRpkgUB8lzfbJUUypV+Nk/+7MsLi4SliJ+4ed/np/7uT/Hnbv3+P4bb/KRj3yUKCrzpX/xJf7aX/3rRFHE1atX8XyPUrmMNvYIVpywQcZYTpxY5Rd+/q/wp//kn8YYw8lTpzhz5gxXrlzj3v0HrJ08xcc/8Un+8T/+v/jkJ17ixz/144zHYxzHYWFhAS/wyPLkMORaLKUoQinF2dPn+PrXv46nIAgi/uSnP81wNCTPcyqVKp/4xMf5zmvfAeBTP/4pfuPXf52t7QcsnTjBQfuA7e37lErVI3d/QCmB7wdcv3mDlZUVRuMxzz33HBcvPkV7f4+p6SnOPXaezc1Ntra2+cmf+knefuttvvv6d3n7nbdxXIeV5WVqtTrj8c4htFBKMRgM0FpSa0zheg43b9/m1OnTXL92k3v37pLnKebSZXq9Pp/+iU9z88Z1dnd3GcUxJ5aWePDgAZcubRBGLsNh/3Dmea5RyiFJxozjIXE85trNW9y6cxeTaazVxGmMsZZyqcT2zjbD4aggOvsDpJK4rsvIjuj3BpSi0lFKRDCOc2ZnpxgOumxujiZR0cFoXRDGcYzjurR2txkMBziOwhiDUhIhJQKBcizVWpXt7SPLMjc3x8rKOjduXKFcChhPoEUY+iRJgpQFrLAWcgM6TzG2wPBhEJIkCUo5SCnY29/j9Klzx4mFVmuXVqv1iGn746CKhwn6g8z/3Pwc01PT/wFCi/m5OWpHocVnf+WXuX7tCuVyUbOQAjzXpd/vYxEoxy3uqcJiDUg1ScJOUbsQUmCtpjfoc+rkWT73ub9XDN5qtbh96xoz03UODjpF9kbQHvRxHRflCvIsRoUeRhtGwzHlcgmtNUkyevRb5Fk2qVlsHEILozUY2HqwRbvdm1wXFb7nEccJWIuY1CyyNGVmdpbnnn+O3/q/f5tqrUoYhVy5chVHSSxqwslMoIWZQItyucxgUMDjIpMrpCioVKUKti6Ox3z0ox/h4KDNaDzkySef4Nbt22AtlUoVay1JGiOPQotypYzrOHhuhyiKcF0Xz3VRjlMgrjxHCMna2ipLS8v85m/8JrVanTRN6XY7TE9PoxwHIcD3fHKdHWaiNEkZDgc4rovnuZOTJx6xRkVgc3nqqae4fv06e/t7rKysEMcxea5pNBu4rot4tMxHoEUcjwvyZrK1DmsWBWbR2lAuV5ifX+Ddd99laWmJ+fk5bm/eplqp4LoeWIvru2ijj9csXD9EG0izrKCeBLieQogJeVyvcfr0GYbDETeuX+fUqZM8eHCfOE6oN+okSUKuNSbPCDyvqMg8ghZ5TpplxUOUAmM0xhgcR+G6LpVyheeee5bXXvsuU1PTzMzMsHl7k1Kp9CiuSClJ06yAIuJo9reWKAqRQhZfShGGIVGphKMUy8vLOI7L9tYWTz75JIPBkDTLmZqaQQiJFALHcaiUy0wIpsPdYrQpLlIChCzWXElFKSpTiiqcPXuevdYerqvoD3q89dbbDIdF+gvDgDgpCoJK2klY4DD7+2ERs402BRsnJJ7vU6lUWVpeZmlpmfcvvc9wNOZHf+TH+Gt/9a/zV37xv0MKwZ07d7Da8uQTT9CoNyaDy8McWqQ7ByE1rusTRiVmZqap1WqcO3eOTqfDzvYOf+kv/Ze0dnfp9wf4fsDP/dyfp9/vFZnfc/naV7/M9u4OPDyhQgjyLJtgwZBqvU6lUqbZmKZUKjE/v8h3X3uNP/Nn/lN+//d/H0e55Nrw4MF9oihibX2Nm7dusddqMRz0AHkIRI21lMsVfD8gTXKqlQoL87NEpYj19bXJJWqdb73yCnGc4HlQKpVxPY9Wa4/d3R2EFMTjmFLkEx3NocXlFILAY33tFKurqyilCIOQ5z/0Yb785d9nOBgwPT2DQHKwf8DG5cvMLyxysN9mt9UiSWNc5TI9vU6SJcehxXA4wAIL85I4SRFItBa8+94GG5ev4Pse586dxeY59zZvE4UB9+7d5eBgn2qtTqlcweQ5/f6I4fAI4tJ5wZmMR0Nu3LjKcNRDSYfAD7l29TJG57RaPe7fv4+rHLQuqu3DeEizWaNaqyOVot8rwkUYeMehRTzOmJ5pkAyHbN+9i3JdXM9Ba43nemRphuM47LfbjEYjPN8lDH2UKxkOuhhrMNpQr1W4/2D7OLRYW1vn6pUrVMoRg9GIPMvwA6+AFuIwQuZGYy30hhohBWEQkqUpjnIePY9TZ89/EFq0aLVaGK2LiuIfA1v8UGgxN8f09P8f0OIHzfoofPijCiKO4zA/v0C9XjtaEPllrl3ZoFopoyeZxHUc+oMBrgRXKRylUCLBxIAWGKHw/BAGB9QDnzz02O0lLJ45w9/63K8egRY3rzI9XaPTPsBxXYyBtNvFEQrHcTFpjBuWCeMxSyQ89eQyvXYPLSVPvDjPH379Lb5z4FNuLnLr0rsfLIhI9lq77LcOCoJAukQKrBQMRxJPCkatDj+9LPnP/6t55k6WufFrV2k8NsfcqQFvfKvNlbsea7VppHCwxhwviDSaTXqdPlUnoOwphCvBc2nGGe445nR1yM981GPp+S79d3uU/SFzFw7Ip1IasWUm8BAS4nF8KIcAKEdlXCy+41LzFCVlyHyPk5HgU6qNCNtc/JDD0mdCjDbsf/s+00/NQ7tH56pGCsuKrxlaMKFHbvThCc3ihMymBI5LLfQ4kXZYXh7ytGc429ml/smA8p+KMCj0jkO4PkX56Sp3/+E219/LWJ8NONeBa+mYUc5hmRhgnMSEfoAUki6GaMrw1PKIE1GP2oxE6oTswRDb0Qzu7BOsudhgj97OmKyaU3cMq2csC2FCnpoirz5c83LgYu2IVCT0jWBv4JNezaldcAguepi7mnzHQ3c99Cihet5ioj6ccTn7Y2X6MwI9pxgkY6LQB2sOl0UkMUbnCCmJUku536VSHeFVI/JzHq5IcCoV0mAK/5kScnEfM3SofNQn/4ZG7wvanYy+rpKKHMvR8pk2hKUItzPgQtTiL3xGMutXsSbDa+SYF2eR+Sr9uEN1Lid9v0332w52S3P9muE73/fYrEXUpprsDcbHEVcqYGhziPucdlPC2yN2v9lGVTTYBERAD4n0DF6zC7bC6Esj4jdgdLLMe2FES/gg7URpcVQOIV1kq8ezpS6Pl0dgBiz/hTPUfuQMJgxw6vN091pU1k7AA038nqb2XAXRz+heyjGuwrUpOJowCo5kf2uRmaZuNU+v5qx/SNKYq+HPdtFqjKku0rs6ojxXwxfQ++IA+8Bg18qIwEFv5jSsQFuLm0rGSX5EJSLAYAkEnFgMac6XSPsZjtFIvYDvXgThUVtfIL65TXwvx10EtZww92GfhdBnTgiMDBgkDsNYH2YiISRuJSIbDYldHzEbEkjDeMtQ7q6RSR+nsYgI59lz3yf8ySmCuQxG0L6bIG0V6gEMNDrPqVTLR64tRiMCH+HNsBsbeqUpmo09PH+O3FzEmphS0+Hm1U329xucXVugd+2A4bs+d3qQXAg5Fyvc3Q6bjkNu8iMzl5LBKEEJzf1tePsPxkwtBHhunb3uJlppHKe46wgWuf8Hd+nclXhhFeouTtIlHw1oxyNGpWkGo9ERMseC6xaipM2DmPgNQSYNSbpBrjUIyHOLUgHWDKkECuuG2G4HpzNGZhlDK9mTAcJxiILgeEGk148JpxukpRpbjkJj0a4mT3NczyEexziOjzUeu2mMayVpLhBehPAk0nGRrgOOR697JOTOzMywtLLK9evXqJRDjDXkaYofBMTjuJiFUiCG5FmGmWjthJAEYUiSDJHSwVGK3jjm5KmzLJ74AQURnRf8+AcT9VEI8UfBCykl8/Pz/x6hxcHBAffv3y8y/5FZ/bBZ/7DvOY7D4uIizWazGLzdbvPZz/4SV69eolprkGUarMH3PbrdLiCKCrlfVMiNMTgTpaVyXdIsLSQqEsaDmPW1U3z+8xPWYnt7m5s3rjI722S3dYDne2ANnXYHx3UI3JA8S2lWQpaDMjutbUKZsVByqHqG0WhMtRpwZ7fHrVqTmzeuc+doQUQKh53tPfb2Wo+K3EHgk6QJMWMc6xI4ghcbNYLFKbxkyIXlMs1On1xoagsRv7XV4f3uHqhCyfMoWWR5TrPZoNvtEpUiSmGIwBBGAcLkRMZjreSzPh2x1FxgfHeDpSc8Rt/YwpU+WXdEkmYIXLJxXGCfR0rLahnHkfiuSymM8D2PUuRTn6rTRFLrxzwuBCvVKrULU5TOnscMN+nu7lNZPYPODN08wbqFmFLro1qLNKE/6BMGAYHv4zoOvvKQac6MVZxzPVZ8FzkYkA7blE/Vye/tMkoUVoEdZ5RqU3iuh9F5QWU9nPl4NCaYxAQxUY5JC1NeyILr0XQV0smwfoY718TeusP4zS3cakhpLYCqhyrXiPyILEuRSh3OPAwLpWWWJuQmI1CSmiNZDHxqYYAfKirKUK25BOKA+PsbjLfHBOfqOCtV1FSDqXpInsQEQXQYcqGgQhyhsY5AIpiJQuZcxZSwhNbi65xGnmFUHz1Mkbf30A2H+mPL6PsxYhyAK9HakmvzAaUllmqjhlESVyoW6g0WalUanktJp9QwyHGXYC3EPHiAbvWonJtFVEIGt7rEjse10QC3XEZOLs6HlEiWkyUZyigQmvaoTTsZU8KwKmNqWZvogkLaA7I3rpBXLf4TS9COQbvc6g7odIfoTGMoTvGjB+oHPqPhEKUtNs0ZD2JEqpm2OXNRTPMpRfVH5hm9fhVzt4d7vkZwdhZzv0MiIkaqTF/7dAZjHMc9vubWWnzPxUtSylLQdBUvLsyzPOcSnK8jq9uk77yLvr6DXHFxn1sFMhgr4qDGm1s93mkPcaIK+aCDlPJhQQS0MbhYlnzF+arHxZmAC4shzQ+fQazVSLd2MNsD3BWJ8/EF1EyZ5EqPoTdLp1xhM+4zlgLhOmST2pwziZuUKxW8fMi0Z1ms+pyYLmPrHszPYIc9ZKOJ81yGVglq5SyD1+9hdudpR1Wu398iyIbMqJRe7lCuVI8vixSScqXEk0uLPLk4ReS71M+dR9UXIPRR9RCyuziOJL2WIGOXvLFAv3VA1Qv4yMl1pnt93h573NjpFWG5SE2CwaCPk0OaVsgPeqTKo3X1Fjcub5CJHOUJyuWQdDDg3rt3EXmAV95D5zlhqUJoYqrjmO69A3q94eHMzeTOOUoyvnOrQ6vpoRQ4d7ZIspRM2EcEg8KSYEnzHM9IsjwnN4Y0G3KvNaDrNSiFpUPEJYSgPxxxYnGRB70hrU6K8h18x8EYQej7pFmM0hJt4KB9AMZg00KGhSuxMsDUy1TKTQ7uPjgMuQsLC6yvneLK1Q3qlTK9UUaeZ7iBT5LEKKEKKC8gz1N0bh7V3qJSmXg0QkqFUoLt7VucPXeOlZWVw+y/tbXF9vb2o4LID2uDOHoujpYVeMTwKRYXF5mbm/v3BC329/e5d+/eD1WH/L/Bi4e/hVKKEydOHIcWv/zLv8S1axtUajV0niOsfQQtpBQTwYCLmggLlJTkaVbI3PKCYLbW0huOOLV+hl/91c8Xx397e5vbmzeZnZ9mPOpibYqxKb3ePq4rcFxDGGhOnpyiFEI67jM3FbK6GDJVzVmcjcjSPsNBl/nZBlevXWZzc/OQtQDL7s42e7stHOXgeQ6e72HtGMg4eWGOZkVz6a2bLJ9Y4y/+F3+K+w+usTA/x907e/yv/9tvY22JYaeLQhzm0OIgwdTUFIHvUyqXqTfqVGsh1UaF1fk6H33xPEFQplRu8tFPPEWmhzSnlnj6mU+yeW+TtZMLTM9VyXVKNtENHF7PSyFKOSg3oBSGNGoBS/N1lmYjfvRHn+DChfOMhjHnz57h/PlT7O+3+fCHP843vv4yO9vbPH5+lfkpj+npGo7vk+f5kRwax2TZGNf38BzJ0okSp05OkcZjVtcbRJU6o9GQJ554ipmZKVZWF1CO5eU/fJl4YFlamOexc8t869t3uHWrc1hWKOqhCZ7vgMixImd+fprBcMi1q/cYDlI2b99GSPjIx56iVgtZWV3hnbff5f1Lm2gT0u7sohxNt9shzbKC4D+UcQZkSYpOUiwGhMfNawM6HUu53GA8HnPm3GnmFhokpsdoPOJ3fvsrZHFOs2aJvITbt+9ze3OXSqWMlOoIf57nKJHjKkUUlLh+bZeD/SGlSs7C/AzSCZkfCFynzsJchbffeIvL773Jx188x7PPL7E4P8Vvf+ld+v0EJqWFYzm0NmltMEbQ62QsLc3y0p94mk63z/LqNGfPPUaaQDnyaHfu8Wc/83FefOE5cgyXL9/mnXfvMTe3wG5r/3C3PNTPpbkmSTMiXxL4mpn5BkvLS3zhN3+bQW+ExGJMzH7rAbVSyKd+/JPcvH2V3/2X3+KffOFb9PqaLM8xE2GIc5j5fQbDPo4jWF2bYWamiXI0v/svv0QUNYnKFTY3rzI9Pc3vfel3yPMhs/N18twhjgWjcYZQMBj0UMpF8EFo4TuUyz5xOqDX14SRw907m7zwwosILH6o8DzDkxcfA5uzvbNPt2+4dv3BREuXEYQVOr3kKLEg0EbjIbBpRmvnDrs7OdZKHCFYW5sjS2OWF0+x9WCTg709dlq7dPoJb791vbgghy5prpHSIcsGx0NupVLB6CElz2Nxrkm5ErB30OH06XXW11ZpNJuMxglvv/UOb731BlG5wjDNQSUIOSQf5zi46DyjUikfH9wAtWqNx8/UWV9rMh4PObE4w4XHH0enMbVSxDe/+TKbd28SlX2CyKFSD6lWJFM1H2klnZ6mPaywtdP9AMXd6yNR5NrjoG+QRhO5Afdv3eCusdy5fpMrVzcYJUXNdNzXOJUqFb9EbalKlmcYDrj14IBud3jI5QJFfW0c8957LeZOlPCkwHfGZOkOjitJkmu4nkeSCazNyNIRQozI0hRtDeMkZ2e/jxA1yqXSYRFKSsl4GDM7M8VgNCC+m0zKxAptBEHgk6ZFCksyQb/fQ9gCdkulQAqs46KCkHJU4e6drUN52/z8PCvLq2xsXKLZqDEcZWTZgCAIisIrIEVB6GU6nyi8DWoCLcZJUtyBpGBn+w6nT549Di12dnZ4sPXgUVXxaG/Fwx6WD34eqhuOfv8htJidnf2PE1oUsvtup4sQAtdzCo0zBSJTjkueZXiOyzhNcB0PkVt64wHrJ0/yuYcyzmPQYlhAC0tGt7uH41lcz5LnCQiNsZphr42rB4S6S967T7MkGQ97dAZ7NGdqXLl66QdDi/1WC6UcXNcl9DzyBHSe4HkO7VaP9qDP6cUT/Pgzp/juy6/x+NOnaZuA3//qK9SaJYSjsMIhy7Mjx99AszlFv9vDDwIqUYQvUmZqLrPzK9g8Y2Vpns32iLpQtPcfMDPT5OMf/Rh/63/6h1TKIfVavegPzdKiAn8ILaKixu85lEslpBScPznPX/lv/jyrK4uMc5fXr2zRPRgQ5SNuXNngT/7Ux/jeu5e4fm+PuRNLuK6DKyWBFx4HRUk8Ztjv4XlFqdLxDDMzDV75zlW+/cYdtvcsvR4sTTUpBYIf/cTHmJ5e5MHONmfPrhblMsfHmII/N0ehRZKkE/hm0UbTqNS4dnufr776DokR5DrFs2OeOrXIbLPGhSee4NK1W7z//vv8yIcfo+xKrFGEUUSSfwBx+aFPEqfozCC0RmlBL8nJhEF6EitTFpsOUxGkyQAhDL/75Ze530lZmIuYrjvESUI6jgknaPjRzNMsK2TI0sOVCmsM/UGPPM/I0hTXjvnQxcfITcramTPcvtvm++/fQYVT7LXaeK4EobF5hvmg1kIA1WoVZBGkXNeZoF2Pca/L+nyTj3z4Q7hRmdXTF3j5tffRQcDcXA2rBeM4w3El9akGQqnjMk5jIM0zEAWZFlUV840SkdZ42ZCPv/gCYeQyOzfD9u4Bf/jm28zPzFLzHFzfozNMkCogTlPsB1kLz/MKGac1xHFK6MPHPnSKrH/A2olFLj51gVbrASdWZ/nSV79GYiQLVY9GKef21jb9bowwOb1B/19nLcAS+CE209SrIZs3Nykx5LmLK1y4cJEwgnrjNAf7HV759rd5/OwyZxcCciW4dr+HsQlSJJSiefq9I105QghMniOFoFSuUK54RJUSW60u5WqFD73wGJnKWVw/zXe/9SrPnFnmf/yl/4wTsz6bd9uMRlAu+0RhhKs80iw7HnLL5QpGj4kCl9npCuVSk/7BHtoaXC9iauYEm1fe4403XqY5VeH1N15h++CAcqQYZuOiRCmLSFmpVI4PDtCoV3nyzCwrSw3yZIxeqPDs04+D7VP14OVXvkIlBN8TtHZarK0vUZ/LmeuPsNLnoGPpjlzufxBa9AcDJC5pWmLQEyib4auEnZtX2L9j2Lq2Qbt1h/n5OhofZIDFoVwuUarNkFsHk3e4u7VDr9c/pESKDm6fcZLw5uVtFmcrBA4ESrCRt3F8iU5v4AnLMFNk1pLmCVp0ybKU3FjiVLOz10EQUCp9oENkMEqYnZ9iOOqwuTOYHCAfawtNdJqC5xQUU7f7UFsxAGxxwQWcoEGlXGZz895xaLG0tMLVq5epN8qMBkUPehiFjEfjouSrCumaztOik14bHKEolcp0BiOULLord7bvcvLU6R8ALR48wJj/0KDFeDym0+n8QBnnD9JW/LCPUoparU4YBsXgcRzzy3/jl7h+Y4NKqU6uU6wpdlCv3wVhcTwP3y3idJqlOE6hSpBSFaJhzwML/W6PkyfP8Hc///lDIcfNm9epVkrsH+zgOA7CWAa9vCiLqYjxWCClR6wlw1FOsxaR6RFJkhJ5Hp39FrnWzMzMcP3GBu2D9mFBRCFp7bTY3zvA9Vwc5eF6HjZzChsJV9HvZhgxx9zSc6yszfHad77CqdNr6GGP773+DqVJt5+j/OMhN9c5jUb9kYSzVmsQlBrUGyeo1ZqUShVmppeIahWeeu5p9vp9qvMnOfvEx7i7b/Abp6jUFzFWMBqPDjuhita2EBC4vk8QRkhHEkZ1/NIy0mTkOiOXPrPNOoaYy5fe5MmLz3K/HRPLFWZONCHr4KhNXDU8foNOk5gk0ziuh+cHKHyMyUl1jsDFiyJyrWjW6zy4dx/P9ahUy/T6GiXLNGZPEI96+ElKPLp5uCwPt6LneRMNdOGXkIwlOnew3iyqsoYqL7K4tIzJck6sLOFXIrbv7VH3LMQDsu5dhByjdXZEUgh4QUSaJeg8Jckt1g0Ia6t40SyVKEJGNWbXTmOyMYPxgOXlE0hZJ8kCrMgZja6SZ23icRcvUIcqkeKBGvIsx3U8lArQ1kOoEtq4ZEow7B2w3PTZ3W0T+TC3tEovbaKHY2SeEvjTeMrHGArNxtE0lxtDtVZHyQDHCfHcAG365FlM52CfqpuxvjzPWKfMT9XoDwTXr+5Qq1bx/QDPDXD9wr7i4Yk+jDjakucW8LGygvHm0ZQRlTrj8YizZ9a5t7uJcRKmFk/w2suXSdtdomqIlTnxeIss7pNlYx5GEOchJPJ9n1EcY71pjKiTuNPYoIKozzDlJMzO1Xn90hvMViq02xZUSDm07O/vUA4kVluydETf9vFc9zjfYsnwgjJuZQ6vvEjuVsi8kM6Duzx5fhUpM0qBy9z0DPudnIWlZTw3QvmSOBugTY50fMIwxJoj/aEIS5pmeG6JsHIC6TYIZUTkh1RLGedWTxAPMs6snKQ99LHeLCXfwWYZ1rg4XkQQObheYctSNOAc2YrVahmQOCqiUvOYmy4RCsO59SWm5xewhMw3lrizG5CmDsNBl9DrUYtGpOMWSTJC25hcp5Si8tEOkaJj2/UUc8szLKysk6aGUZLy/DMnedA64OzZZd568xZ+4OIrjVsOqS2tMBh0qA5CpE0Zt+9iRvfpd1vHH+hoECO9AzK9R6dTRRsXHIcrN1t0Ox322wm3Wx1UqsmFxChD5jmEUUCpUsJmIw6yLnd3NxgOh0e1FgZHOcTjHnc2XqU+cwchFI5f5f4Vg/QC7mykKM/FZhnYEXnapa9swebpnDQe0+3s4oiseKhHCyKDccr0VJPRaJfe1h6e6+B4HtYalBtAkiIct3Aw6HfAWnKtkaJoNsAaSoGlUq6zvXOk+bRWq7G2dpLr169SqwYk44TRYIjrFTV9JdwJ16bJdIqZuNJIXMIoJB5lCGGQQnGvvcXpc2eo1WqH2X8wGNDtdrFmYgrzb/kRQlCv1ymXy/9RsBZ/47/n+rUNatUqOtcYDI7nFv1C1uK5hdZCKEmWFL44eZ7j+yFpGiOB0AnojAasnz7Fr37u84dai83Nm8zOTnFwsF90rQL97gDHdZBCkuuUyI3oDsaEpSovfPhFdjtter0eSydW6RzssfHW20zNNLly5f3jrIXWlt3dFq3dHRxHTdo5XdI4RluBVIp29w6nn3iW//qv/hKPP/Y4X/y9L1Oulvjwhz7Ob/7WF/jdL30RmEYpQX6MtdCWZrNJr9vG87yJQ1VxP5WqaOZI04THH7/A+QsX2Nra5qDT5pMvvcTNW9f4Z//o16hFHsqRjMbx4W3OWku1VsJxFI5TuPxIKSeGPB6BHzIYxuAHnHniKeJM8/r3v8/y8hrCcfneq39IvN8i9D0QEt8LjhdEkvGILLX4foDv+UglMQa0sfTHKaXGDOcuPs/5p1+gN+iDVLzwoRf4wq//E37tf/kHrM7N0B0egAVrJ622x1gLz5/0VhR2K6PRCKM1yg1wgyo/87N/jrnZOe5v7bC0uoLrwje//HvUQgfHL5bQm/SZHmct/IA0SdA6ZRyP8BwHJT2MCAijacaxZmtri739fbb391hfW+fu3U0O9vapV6pkaYwSgjROiILoOGuhM43WGsd1UEqQ5DlBqYoTNKmsn8WdbrJ4YoYsi1lfXqbSbGJVwJmT65M4I0ji+JFLxLFMhLBUqtWJ1lyCkuSOQ6wV1fkZ/uJ/+/P49Sm6/TFnVld57Y03+N//j/+T/Qd3CUMXCzSbTaanpxAfNJ7KbdG5ipVYI3CEw2gU09MJgySjFNVIU4sRAuG4vPP9N3jvla9hyLCAzi1hVEJrU9z0jsqVPc9jFMdoY7DGko1iFhbXefE/+QznL15kvlrj2pVrnD61yP29A7RRNOpzKKsxaReIuXfvPpUoxFXecWghhMCRCuW4OEGAjEKCao1PvvhRLqydoNffwwsUc1N1fu/rr/D1V19HRA3wqgjHxwqL63iUy2Uy80HWIstxlEu51CSoTaFKdeZWzlCrBjRDn9Ggz8nVOW482GG7dcDSyXXK01PkWBJtCUsNoqCCkEWC+QBrUSY3GjeqUp9eBj9iJEDbjDROKLsBp5sNvnprg80r17HJkEBK/FJIvyeJU4tQDmme/+ushcVSrlVZOHuRhbVzHPRGVGoVphrTZHHChbOrbGxu8Wv/6J9RdiXTsyeIwpBMJ9SaTdCC8WAPNdplf+f+0cEtnX4XI8CmGQedA1INyVjx1T94GWkl9VKJb377O+y3WyyeWCSzGqTEUyVOLJYxxrK3ZdnaukW3e4y1KCz00mTIzY3v0WyvooRiGPjcePdVQJCklpKrmHYVg/s30GlGTwiyPEfrjDhJGOzv4AozaVU+wlrEw5i5+SlG8YD2/ev4roNyXbS1BJ6Hk2UYpUizh6wF5MYgxERfagXVAKJymVu3PsBarK6ssrFxmVqzyiDrTVKYT5pmCDtxGxNgco02GmvACEG5XGY0GBRtWY7kwfYOpz5YENmdsBb6PzjW4t/UMPOHdYn8EMPMv8H16xtUK4WwDqFw3GDSrgaucgmCsKBStcHzCx8ApQKyvCi0Wlt4uqyfPMnf/dznjxhm3r7J1HST4WCMMcXtoNc7QDmFYabWGQiDsbqo+LoOwhVYFSOcjP6gy2DQpdmsce3aBnfv3jvqayFo7e5xsL+Hoxyko/ACHxtnYDVSwHA0YjiOWVyc48lnH+ebr36d8+fW2by1w6XLV2jUaoWnVJFID0OuMRn1RpV+t0sQBARRgFCFf6g1GkvRqKcCwbMvPMle5wFBSXHy9Blef+39wlSzXJ7YxqWHllkPtRau6xaGGWGA9ATVpk+pGhKUS0hPEdsxtekSzfkGN+/eYHF1mhubN9nd67BwYg7fcVACXNcrfOkOWYuEQW8wKVU61FdLJCKhn3dISl1MKWNoR6yeW2ecxczMzFILp9h45zrlUkCjXkPawkLO2iN3fyEEcRzj+t4Em4uiaaZuWHyqzCDr0R12qdRDllaWGKdjvKjM25evcBDvU23WSdIMVRZ4UUCWp8dZi8AvkSUpmdYkSUqv1aay6DFMYgbblihq8Ozzz+II2NnaIstjvJLECyJkXWKnx2RuzjiOCQP/EBQx8aPQeY6SgmDeZenJJpF02bk+YP2Jec5emOP5x5/nYP+A6ekprKO5+2CLfv+A2iJ0Oz3iXlEwzLPsA9dzqymVy7g1S20lIpyJ6GzF0Ld4oaAWzlCOaiDhxPIJOu0OIsiZP1dld6uDSB0CXxVZaGJXcfhAc4N1Lc1GndbNfeKhQVRcZF2xtXnA+okzbO/e4/TZdca6w+UrNzmxPMX82hzJSFItlxDWonPNQ9+2ww4R36ffG9LeG+Ean9vv36ff6RKLnFpzmrmZaYSwSBSXbr/F+Y/NoTyXBzf3WVqfxiQSEStGgwFKig8iriJ+ZJkhGeek3YR4bIjtgGeffRLPDZiZneH7r7/J9t4es2sNWvt9zNBndC9HjzVaGzzfI9fHLLMExhhc10EKCEOH1PHxpiQz9VmeO/8cNrUEpZBbm7dpjQb4YRvRV2TdFM96RF6JfNrH9X3S7uCw5ccYSykqYUVCVHPwyy5KueBoPv7Ux1idXyXOB2zeu07u9Dh5coGsq/CGFF5zORjHEgQeaZxRLlUOJSgPU1ipGrKyXMcNIuI0RVUVz559ge5gj6WTi3zt5ZusLZ4DBB07YumkQxgGCGvwHJ80g+7OgHbaPd7CORj0QbksyCkcqXACh4pT481X32Zvv0W5EnD31gNKYb0oeE8FJJN6kOsH1Kt10tiwdXO3cDE8NMwsTALjYcrVd+9QqpVQrsR1euT5bQSWOOnheRFDkUxyrCS3hQGyqxT7qku3PSQZp4RhCPbIAx2PY6ZnmoxGfeJuiuMqjFO4/Hqej0gjrHGIc02/P8BaU7imKkWswNoRAkmtXuPB/e3jllnLK6tcvXqZWq1KPPGR8/2i+RSpHlENWueTSovGEZJSucRoOEaooq28tbfP6ZPnf7hhJvaPx1uIQkl0vKzgKObnCsPM/yiaTz/LtWuXqEx8LbBFRWsw6CMmEk7XlSgJxijQIGThPJ1lGX4QYoymPxxwcu0M/8NRN86bN68yNVVnOOihTYbWCb1+B6kESkKWjYtbWq7odQ9Yr1U40Y85OT3Ni+cfY3DnPjvb24Shz5Ur7/HgaPOpFJLdnV3ae22ciYu17/skcTbpHRLEw8Jc8MR0g6jbJT5o89JP/ineevcdbm9ukpcjypUyUgaYD7pxTk1NMeyP8H2fKAoLlyRHFX1YFtzAw6s4PL9yksF332KuVEb3Dvjuxvvk5RKlkosUljiOUepI82nhlSBwHYcwDFGOQ+D7OKpwssrzjFznnJk/QTPO6ey1mS9V2NrZZWs0xot8wnJYmPpGEVof4RXTJCFNDK7n4XnuxF7IgM2xmAmxqZivNLn+2ltkJqccBbx54y69ccLS0hzaWLQpANYjq0+A0WiE67k87EYTovi7XOdFcSRJ8P2ApD9ga3cHvxywnQ55/eZ1Ss0yYRihtSEI/IlPkTjuxpkkCTrLyJKkUC04siDPHA8ch7l6jdbmXazOEMrnW/sd9hXUKzV0loEx5GlG6BcM6WHNQufoLH9kPZHnGSYvrPdGwxElx6fm+hxsbfP4wjKJtVyJY6qliLJysQikEIWLWJ4fai0ecolRFD26k1ormNiK0R8Nma012Ll9D98UN7h7+RgTBNSnaqDMo2BTrpQnB/8obtF6YjBiMJkmixN0qsniDFdZKpFP6/Z9MiF5t9Niy3PwIh8hXbLUYLIMoS35hCU67sbpOAz7A2ymIc+RZtKSMk440Zwh3u/Q6XZpG411AqQbUAoCsjjGJBmOBpmbR+bIHCvlCIHvekhjcRD4SuErB18qZupNNm/cwqgi2XakRXouZeXjakngeMgJfCuHUdER+MgwU4qiS15KQtel5Pt4UuJow8L0FN29Drt7ewSBgxUGG7hUgwBHKGyucYWg5HuEnoeLR5YcaT6VQlIqldCDAYFy8JWL6ziUvYBaUOL9K1cwSuFkmrIX4ZarVCvVQlkiLVmSEQgfUJgsp1Q60nxaiMQEfhjgZNCMKqAzjDa88fbb7PQ7lMoRAw0NAdNCUUISW4OLg1+OcIKAvu2RBC798ZHmUyUlo/GIMM9wdlvQ7mGAm/v77PW7BJUQneb0hUSMBwy6XWKj6RuDEVAPi5qS53n43hSj0RHcYqwtbINdB6dcpm0Mw3RMKg1r0/PENsdgGeqUnrEsz8yz0KgyIqffH+DkhjAICt9L1yEMow82nw6JZpr4Z1ZQSlERgqlxQpbnKOUwGo1QgYdCYnONdR2cPGWqXn7UjSylxEqP4X77MOTOzMywsrLK9esbRKUItCHLNUEYFa6/lgIWj5PCsm9ieSMQVMplRvEIIQWOoxj0x5w9e57FhR/UfKr1xNju386NU02aT6f+vTWf/n/LWhyBFg9f83H9+hUq5VIRbq3AfSgbn7wSwfO8Ry6+nu+S5TECB6ML6GW0ZpTGrK+f5u/8naO+FrdvMzU1Rae9N/G1sPT74wkLZMi1puKXiJOMg4MDyuUy1ujJqygKPtcYQ3Nqio1r73Pv3r1Dv1yLpdXaZWdnj+2tXdoHbcbDIVkSMxqOadSn+NjHPsnr3/seZ8+fp9frEccp9+4+4OCgze7ODjvb27T3D5B8wI3TmJxGo0a/2yMMQ7TRXLz4JL1el2q1xmgUY7H8zGd+hscvPE6tUqFWrfGFL3yB+YVZer1e4RxJwWY/IucPZZyFys/zfU4sLrK/v8+JE0s88eSTfOITn6BWrfLMxadxHYeXPvlJsizlF3/xFxkMBozjEWEYFaaPbvADWIv+ANfzKEURjz/+BBcvPs2FC08wPTXNoN+nNrFt7nY6bGxs8NJLL3H9+jXSJOX8uXMTCafCmKLD2znKWtTrNVqtfVZXV6jWapRKEbu7O8zOzvLRj32UarXK6uoq2mi2t3b4gz/4OmfOnOXMmdO8+uortHZ3mWpO0x+Njqv+giBiNBpSr9exFh48uM+5c+fwfZ+ZmVlmZmaQsrAAzfOC01pbW2av1eLB1n3effddolJEksREXnjo3P7Q8UebHMfxmZub5bHzjxEnMfNz88zOzDAcDimXI9rtA9I0JU1jfN9nb2+PtbU1VlaW+erXvkq3u08QVoq220eDG0Oj0aTV2uV73/seURQhhXxkKh2GATs7O1hr2dvbI4pKHLTbj5Z0t7WLowSer3hIkDpHCyJxmjA1NcWP/diPsXHlCo7jUG/UCcKQ9kGHNNW0Dw7odHqkaUqt2iCejrEYkiSe/Ls9VCk6Di081yWOE1q7B3z1K/+KZqPJYNDnzTff4vKlS7Tbbfq9Hpt3NhmNRkWddPIM7tzZRCnF5UuX0MYef4nAI2GBo/B8ydz8DO+8+zaPP/44BwcHJFnC7c3bxa+/W5zemblZtrYeUCqHnD59mrfefJNca+q1xiOVyKPXfFijcWRhwX/p0vs89tg5trbuMzMzRZKM2d/fm7woYEBUikiTGItmb2+PV175NmFYQkpFEARkWXKob3n4mg87YXrm5ub4p//0n/L++5cYDAa0D9oopQiCkIXFeRzHIUkSrl25Qhj4OErwta99Dc/1MRpKUeV4KUdIgR/4pFnySIfVPtjnt37rtyYm6xm3b9+k3d7n2rWruK5i0Ovxr778FYb9EZVKjSgqU681cSbbxXmITrv9AYFf2E/E4zGlKCLXGj2OeevNt8kyzcblDZ68+ATvvfsuyXjM9vYugedz+9Yd/ChEa02n16U/OOLGiQXP8wrzf10k38FoiOt6lGsVBr0Rv/cvv4If+PT7A7SxXN64SuAHhOUKvi3gSZqMidOY4IPQYjQaMTMzha8clFN8Oa6D1jneikev26dUijg42KNeLxNFJbJc47qFV7SdcAFe6HHv7oMPvOZjZZ0rVy5Tr9bIBgnaZHiBW1h8ShchJIPhaMJaWPYP2gjpEUXFu42kLHDLeLvwtfgBrEVrcjX/t1dbHH3Nx79TaPH/ACMWKXn4G2HkAAAAAElFTkSuQmCC";
        private static BitmapSource? inventoryAffinityIconSpriteCache;
        private static ImageSource? GetInventoryAffinityIconSource(string affinity) {
            int index = Array.FindIndex(GetInventoryAffinityNames(), name => name.Equals(affinity, StringComparison.OrdinalIgnoreCase));
            if (index<0) return null;
            if (inventoryAffinityIconSpriteCache == null) {
                try {
                    byte[] bytes = Convert.FromBase64String(InventoryAffinityIconSpriteBase64);
                    using MemoryStream stream = new MemoryStream(bytes, writable : false);
                    BitmapImage sprite = new BitmapImage();
                    sprite.BeginInit();
                    sprite.CacheOption = BitmapCacheOption.OnLoad;
                    sprite.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    sprite.StreamSource = stream;
                    sprite.EndInit();
                    sprite.Freeze();
                    inventoryAffinityIconSpriteCache = sprite;
                } catch {
                    return null;
                }
            }
            int y = index * InventoryAffinityIconCellSize;
            if (y + InventoryAffinityIconCellSize> inventoryAffinityIconSpriteCache.PixelHeight) return null;
            CroppedBitmap crop = new CroppedBitmap(inventoryAffinityIconSpriteCache, new Int32Rect(0, y, InventoryAffinityIconCellSize, InventoryAffinityIconCellSize));
            crop.Freeze();
            return crop;
        }
        private DataTemplate CreateInventoryAffinityItemTemplate() {
            FrameworkElementFactory panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            FrameworkElementFactory image = new FrameworkElementFactory(typeof(Image));
            image.SetValue(FrameworkElement.WidthProperty, 18.0);
            image.SetValue(FrameworkElement.HeightProperty, 18.0);
            image.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
            image.SetValue(Image.StretchProperty, Stretch.Uniform);
            image.SetBinding(Image.SourceProperty, new Binding(nameof(InventoryAffinityOption.Icon)));
            panel.AppendChild(image);
            FrameworkElementFactory label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            label.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextMain"));
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(InventoryAffinityOption.Name)));
            panel.AppendChild(label);
            return new DataTemplate {
                VisualTree = panel
            };
        }
        private ComboBox CreateInventoryAffinityCombo() {
            ComboBox combo = new ComboBox {
                Style = (Style) FindResource("DarkComboBox"), MinWidth = 96, Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 8, 2), VerticalContentAlignment = VerticalAlignment.Center, ItemTemplate = CreateInventoryAffinityItemTemplate(), ItemsSource = GetInventoryAffinityNames().Select(name => new InventoryAffinityOption {
                    Name = name, Icon = GetInventoryAffinityIconSource(name)
                }).ToList()
            };
            return combo;
        }
        private FrameworkElement BuildInventoryField(string label, Control control, int column, int row) {
            StackPanel stack = new StackPanel {
                Margin = new Thickness(column == 0? 0 : 8, 0, 0, 0)
            };
            stack.Children.Add(new TextBlock {
                Text = label, Foreground = GetResourceBrush("TextSoft"), Margin = new Thickness(0, 0, 0, 5)
            });
            stack.Children.Add(control);
            Grid.SetColumn(stack, column);
            Grid.SetRow(stack, row);
            return stack;
        }
        private Button CreateInventoryActionButton(string text, bool enabled) {
            return new Button {
                Content = text, Style = (Style) FindResource("ActionButton"), IsEnabled = enabled, Width = 104, Height = 34, Padding = new Thickness(9, 2, 9, 2), Margin = new Thickness(0, 0, 7, 0), FontSize = 12, Focusable = false, FocusVisualStyle = null
            };
        }
        private Style CreateInventoryPopupListItemStyle() {
            Style style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextMain")));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.PaddingProperty, new Binding("Padding") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.AppendChild(presenter);
            style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) {
                VisualTree = border
            }));
            Trigger hover = new Trigger {
                Property = ListBoxItem.IsMouseOverProperty, Value = true
            };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropHover")));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("DropBorder")));
            style.Triggers.Add(hover);
            Trigger selected = new Trigger {
                Property = ListBoxItem.IsSelectedProperty, Value = true
            };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropSelected")));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBright")));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextMain")));
            style.Triggers.Add(selected);
            return style;
        }
        private string?ShowInventoryAffinityDialog(Window owner, string current) {
            string[] affinities = GetInventoryAffinityNames();
            Window dialog = CreateInventoryToolWindow("Set Affinity", 360, 500);
            dialog.Owner = owner;
            dialog.ResizeMode = ResizeMode.NoResize;
            Grid root = new Grid {
                Margin = new Thickness(12)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            TextBlock currentText = new TextBlock {
                Text = "Current:  " + current, Foreground = GetResourceBrush("TextSoft"), Margin = new Thickness(2, 0, 0, 8)
            };
            root.Children.Add(currentText);
            ListBox list = new ListBox {
                Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), Foreground = GetResourceBrush("TextMain"), HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            list.ItemContainerStyle = CreateInventoryPopupListItemStyle();
            Grid.SetRow(list, 1);
            root.Children.Add(list);
            ListBoxItem? selectedRow = null;
            foreach (string affinity in affinities) {
                Grid rowContent = new Grid();
                rowContent.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(28)
                });
                rowContent.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                Image affinityIcon = new Image {
                    Width = 20, Height = 20, Source = GetInventoryAffinityIconSource(affinity), Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true, UseLayoutRounding = true
                };
                RenderOptions.SetBitmapScalingMode(affinityIcon, BitmapScalingMode.HighQuality);
                rowContent.Children.Add(affinityIcon);
                TextBlock name = new TextBlock {
                    Text = affinity, Foreground = GetResourceBrush("TextMain"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0)
                };
                Grid.SetColumn(name, 1);
                rowContent.Children.Add(name);
                ListBoxItem row = new ListBoxItem {
                    Content = rowContent, Tag = affinity
                };
                list.Items.Add(row);
                if (affinity.Equals(current, StringComparison.OrdinalIgnoreCase)) selectedRow = row;
            }
            if (selectedRow != null) selectedRow.IsSelected = true;
            Grid buttons = new Grid {
                Margin = new Thickness(0, 9, 0, 0)
            };
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Button cancel = new Button {
                Content = "Cancel", Style = (Style) FindResource("ActionButton"), Height = 34, Margin = new Thickness(0, 0, 5, 0), Focusable = false, FocusVisualStyle = null
            };
            Button apply = new Button {
                Content = "Apply", Style = (Style) FindResource("PrimaryButton"), Height = 34, Margin = new Thickness(5, 0, 0, 0), Focusable = false, FocusVisualStyle = null
            };
            Grid.SetColumn(cancel, 0);
            Grid.SetColumn(apply, 1);
            buttons.Children.Add(cancel);
            buttons.Children.Add(apply);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
            string?result = null;
            cancel.Click += (_, _) => dialog.Close();
            apply.Click += (_, _) => {
                if ((list.SelectedItem as ListBoxItem)?.Tag is not string value) return;
                result = value;
                dialog.DialogResult = true;
            };
            list.MouseDoubleClick += (_, _) => {
                if ((list.SelectedItem as ListBoxItem)?.Tag is not string value) return;
                result = value;
                dialog.DialogResult = true;
            };
            dialog.Content = root;
            dialog.ShowDialog();
            return result;
        }
        private string?ShowInventoryValueDialog(Window owner, string title, string label, string current, IEnumerable<string>? choices) {
            Window dialog = CreateInventoryToolWindow(title, 360, 185);
            dialog.Owner = owner;
            dialog.ResizeMode = ResizeMode.NoResize;
            Grid root = new Grid {
                Margin = new Thickness(14)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            TextBlock labelText = new TextBlock {
                Text = label, Foreground = GetResourceBrush("TextSoft"), Margin = new Thickness(0, 0, 0, 7)
            };
            root.Children.Add(labelText);
            Control editor;
            if (choices != null) {
                ComboBox combo = CreateInventoryCombo(choices);
                combo.SelectedItem = current;
                if (combo.SelectedIndex<0) combo.SelectedIndex = 0;
                editor = combo;
            } else {
                TextBox text = new TextBox {
                    Text = current, Style = (Style) FindResource("DarkTextBox"), Height = 34, FontSize = 12, Padding = new Thickness(10, 2, 10, 2), VerticalContentAlignment = VerticalAlignment.Center
                };
                ApplyInventorySearchBoxTheme(text);
                text.SelectAll();
                editor = text;
            }
            Grid.SetRow(editor, 1);
            root.Children.Add(editor);
            Grid buttons = new Grid {
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            buttons.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Button cancel = new Button {
                Content = "Cancel", Style = (Style) FindResource("ActionButton"), Height = 34, Margin = new Thickness(0, 0, 5, 0), Focusable = false, FocusVisualStyle = null
            };
            Button ok = new Button {
                Content = "Apply", Style = (Style) FindResource("PrimaryButton"), Height = 34, Margin = new Thickness(5, 0, 0, 0), Focusable = false, FocusVisualStyle = null
            };
            string?result = null;
            cancel.Click += (_, _) => dialog.Close();
            ok.Click += (_, _) => {
                result = editor is ComboBox c? c.SelectedItem?.ToString() : ((TextBox) editor).Text;
                dialog.DialogResult = true;
            };
            Grid.SetColumn(cancel, 0);
            Grid.SetColumn(ok, 1);
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
            dialog.Content = root;
            dialog.Loaded += (_, _) => editor.Focus();
            dialog.ShowDialog();
            return result;
        }
        private static string NormalizeInventoryToolCategory(string value) {
            return value switch {
                "Weapons" => "Weapons", "Armor" => "Armor", "Talismans" => "Talismans", "Consumables" => "Consumables", "Sorceries" => "Sorceries", "Incantations" => "Incantations", "Spirit Ashes" => "Spirit Ashes", "Ashes of War" => "Ashes of War", "Key Items" => "Key Items", _ => "Weapons"
            };
        }
        private static string[] GetInventoryAffinityNames() => new[] {
            "Standard", "Heavy", "Keen", "Quality", "Fire", "Flame Art", "Lightning", "Sacred", "Magic", "Cold", "Poison", "Blood", "Occult"
        };
        private async System.Threading.Tasks.Task<List<InventoryCatalogItem>> LoadInventoryCatalogAsync() {
            if (inventoryCatalogCache != null && inventoryCatalogCache.Count> 0) return inventoryCatalogCache;
            const string repoBase = "https://raw.githubusercontent.com/Hapfel1/er-save-manager/v1.10.1/src/er_save_manager/data/items/";
            const string categoriesUrl = "https://raw.githubusercontent.com/Hapfel1/er-save-manager/v1.10.1/src/er_save_manager/data/items/ItemCategories.txt";
            string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager", "DatabaseCache", "InventoryCatalogHapfel_v1101_NoTarnishedPack");
            Directory.CreateDirectory(cacheDir);
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EldenRingToolkit/1.0b");
            async System.Threading.Tasks.Task<string> GetCachedTextAsync(string url, string cacheName) {
                string path = Path.Combine(cacheDir, cacheName);
                try {
                    string fresh = await client.GetStringAsync(url);
                    try {
                        await File.WriteAllTextAsync(path, fresh);
                    } catch {
                    }
                    return fresh;
                } catch {
                    if (File.Exists(path)) {
                        try {
                            return await File.ReadAllTextAsync(path);
                        } catch {
                        }
                    }
                    return "";
                }
            }
            string categoriesText = await GetCachedTextAsync(categoriesUrl, "ItemCategories.txt");
            if (string.IsNullOrWhiteSpace(categoriesText)) throw new InvalidDataException("Could not load the Elden Ring item catalog. Connect to the internet once so the item database can be cached.");
            var sources = new List<(uint CategoryMask, string RelativePath, string CategoryName)>();
            foreach (string original in categoriesText.Replace("\r", "").Split('\n')) {
                string line = original.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                string[] parts = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length<4) continue;
                if (!parts[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase)) continue;
                if (!uint.TryParse(parts[0].Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint mask)) continue;
                string rel = parts[2].Replace('\\', '/');
                string name = parts[3].Trim();
                if (rel.StartsWith("Items/", StringComparison.OrdinalIgnoreCase)) rel = rel.Substring("Items/".Length);
                if (rel.Contains("Convergence/", StringComparison.OrdinalIgnoreCase) || rel.Contains("Seamless", StringComparison.OrdinalIgnoreCase) || rel.Contains("CutContent", StringComparison.OrdinalIgnoreCase) || rel.Contains("TarnishedPack/", StringComparison.OrdinalIgnoreCase)) continue;
                if (mask != 0x00000000u && mask != 0x10000000u && mask != 0x20000000u && mask != 0x40000000u && mask != 0x80000000u) continue;
                sources.Add((mask, rel, name));
            }
            var downloadTasks = sources.Select(async src => {
                string safeName = Regex.Replace(src.RelativePath, @"[^A-Za-z0-9_.-]", "_"); string txt = await GetCachedTextAsync(repoBase + src.RelativePath, safeName); return(src.CategoryMask, src.RelativePath, src.CategoryName, Text : txt);
            }).ToArray();
            var downloaded = await System.Threading.Tasks.Task.WhenAll(downloadTasks);
            List<InventoryCatalogItem> result = new List<InventoryCatalogItem>();
            foreach (var src in downloaded) {
                if (string.IsNullOrWhiteSpace(src.Text)) continue;
                string sourceCategory = GetInventoryCatalogCategory(src.CategoryMask, src.CategoryName, src.RelativePath, "");
                if (string.IsNullOrWhiteSpace(sourceCategory) && src.CategoryMask != 0x40000000u) continue;
                if (src.RelativePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) {
                    string[] lines = src.Text.Replace("\r", "").Split('\n');
                    if (lines.Length == 0) continue;
                    List<string> headers = ParseInventoryCsvLine(lines[0]);
                    Dictionary<string, int> h = headers.Select((v, i) => new {
                        v, i
                    }).GroupBy(x => x.v, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
                    if (!h.ContainsKey("ID") ||!h.ContainsKey("Name")) continue;
                    for (int li = 1; li<lines.Length; li++) {
                        if (string.IsNullOrWhiteSpace(lines[li])) continue;
                        List<string> cols = ParseInventoryCsvLine(lines[li]);
                        string Field(string key) {
                            return h.TryGetValue(key, out int ix) && ix >= 0 && ix<cols.Count? cols[ix].Trim() : "";
                        }
                        if (!uint.TryParse(Field("ID"), out uint id)) continue;
                        string name = Field("Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        string category = GetInventoryCatalogCategory(src.CategoryMask, src.CategoryName, src.RelativePath, name, id);
                        if (string.IsNullOrWhiteSpace(category)) continue;
                        InventoryCatalogItem item = new InventoryCatalogItem {
                            ParamId = id, BaseParamId = category == "Weapons"? id : 0, Name = name, Category = category, SupportsAffinity = false, SupportsAshOfWar = false, MaxUpgrade = category == "Weapons"? 25 : 0
                        };
                        if (category == "Weapons") {
                            string reinforcement = Field("reinforcement").ToLowerInvariant();
                            string allowed = Field("allowed_affinities");
                            string aowAllowed = Field("aow_allowed");
                            item.WeaponTypeColumn = Field("wepTypeCol");
                            item.MaxUpgrade = reinforcement switch {
                                "somber" => 10, "none" => 0, _ => 25
                            };
                            item.SupportsAshOfWar = aowAllowed != "0";
                            item.SupportsAffinity = item.SupportsAshOfWar && allowed.Split('|', StringSplitOptions.RemoveEmptyEntries).Length> 1;
                        } else if (category == "Ashes of War") {
                            item.CompatibleWeaponTypes = Field("compatibleWepTypes");
                        }
                        result.Add(item);
                    }
                } else {
                    foreach (string originalLine in src.Text.Replace("\r", "").Split('\n')) {
                        string line = originalLine.Trim();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                        int split = line.IndexOf(' ');
                        if (split <= 0) continue;
                        if (!uint.TryParse(line.Substring(0, split).Trim(), out uint id)) continue;
                        string name = line.Substring(split + 1).Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        string category = GetInventoryCatalogCategory(src.CategoryMask, src.CategoryName, src.RelativePath, name, id);
                        if (string.IsNullOrWhiteSpace(category)) continue;
                        result.Add(new InventoryCatalogItem {
                            ParamId = id, BaseParamId = category == "Weapons"? id : 0, Name = name, Category = category, SupportsAffinity = false, MaxUpgrade = category == "Weapons"? 25 : 0
                        });
                    }
                }
            }
            inventoryCatalogCache = result.GroupBy(x => x.Category + "|" + x.ParamId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();
            if (inventoryCatalogCache.Count == 0) throw new InvalidDataException("The Elden Ring item catalog was loaded but contained no usable entries.");
            return inventoryCatalogCache;
        }
        private static string GetInventoryCatalogCategory(uint categoryMask, string sourceCategoryName, string relativePath, string itemName, uint itemId = 0u) {
            if (categoryMask == 0x00000000u) return "Weapons";
            if (categoryMask == 0x10000000u) return "Armor";
            if (categoryMask == 0x20000000u) return "Talismans";
            if (categoryMask == 0x80000000u) return "Ashes of War";
            if (categoryMask != 0x40000000u) return "";
            string source = (sourceCategoryName + " " + relativePath).ToLowerInvariant();
            string name = itemName.Trim();
            if (source.Contains("magic")) {
                if (name.StartsWith("[Sorcery]", StringComparison.OrdinalIgnoreCase)) return "Sorceries";
                if (name.StartsWith("[Incantation]", StringComparison.OrdinalIgnoreCase)) return "Incantations";
                if (source.Contains("dlcmagic") && itemId != 0u) return IsDlcSorceryParamId(itemId)? "Sorceries" : "Incantations";
            }
            if (source.Contains("goods/ashes") || source.Contains("dlcashes") || sourceCategoryName.Equals("Ashes", StringComparison.OrdinalIgnoreCase)) return "Spirit Ashes";
            if (IsInventoryKeyCatalogCategory(sourceCategoryName)) return "Key Items";
            return "Consumables";
        }
        private static bool IsDlcSorceryParamId(uint itemId) {
            return itemId is 2004300u or 2004310u or 2004320u or 2004500u or 2004510u or 2004700u or 2004710u or 2004900u or 2004910u or 2005000u or 2006200u or 2006210u or 2007410u or 2007420u;
        }
        private static bool IsGoodsInventoryCategory(string category) {
            return category == "Consumables" || category == "Sorceries" || category == "Incantations" || category == "Spirit Ashes" || category == "Key Items";
        }
        private static bool IsInventoryKeyCatalogCategory(string categoryName) {
            string n = categoryName.ToLowerInvariant();
            return n.Contains("key item") || n.Contains("notes") || n.Contains("paintings") || n.Contains("cookbook") || n.Contains("merchant item");
        }
        private static List<string> ParseInventoryCsvLine(string line) {
            List<string> cells = new List<string>();
            System.Text.StringBuilder current = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i<line.Length; i++) {
                char c = line[i];
                if (c == '"') {
                    if (quoted && i + 1<line.Length && line[i + 1] == '"') {
                        current.Append('"');
                        i++;
                    } else quoted =!quoted;
                } else if (c == ',' &&!quoted) {
                    cells.Add(current.ToString());
                    current.Clear();
                } else current.Append(c);
            }
            cells.Add(current.ToString());
            return cells;
        }
        private static string StripWeaponAffinityName(string name) {
            string[] prefixes = {
                "Heavy ", "Keen ", "Quality ", "Fire ", "Flame Art ", "Lightning ", "Sacred ", "Magic ", "Cold ", "Poison ", "Blood ", "Bloody ", "Occult "
            };
            foreach (string prefix in prefixes) if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return name.Substring(prefix.Length);
            return name;
        }
        private static bool IsLikelyKeyInventoryName(string name) {
            string n = name.ToLowerInvariant();
            string[] words = {
                "key", "medallion", "great rune", "remembrance", "bell bearing", "letter", "map", "needle", "cursemark", "memento", "starlight", "draught", "innards", "scroll", "prayerbook", "deed", "mark of", "sealbreaker", "half of", "academy glintstone key", "deathroot", "seedbed curse"
            };
            return words.Any(n.Contains);
        }
        private static IEnumerable<string> GetInventoryIconNameCandidates(string itemName) {
            if (string.IsNullOrWhiteSpace(itemName)) yield break;
            string raw = itemName.Trim();
            yield return raw;
            string withoutMagicPrefix = Regex.Replace(raw, @"^\[(?:Sorcery|Incantation)\]\s*", "", RegexOptions.IgnoreCase).Trim();
            if (!withoutMagicPrefix.Equals(raw, StringComparison.OrdinalIgnoreCase)) yield return withoutMagicPrefix;
            string withoutUpgradeVariant = Regex.Replace(withoutMagicPrefix, @"\s+\+\d+(?:\s+\(Empty\))?$", "", RegexOptions.IgnoreCase).Trim();
            withoutUpgradeVariant = Regex.Replace(withoutUpgradeVariant, @"\s+\(Empty\)$", "", RegexOptions.IgnoreCase).Trim();
            if (!withoutUpgradeVariant.Equals(withoutMagicPrefix, StringComparison.OrdinalIgnoreCase)) yield return withoutUpgradeVariant;
            string withoutNumberedVariant = Regex.Replace(withoutMagicPrefix, @"\s+\[\d+\]$", "", RegexOptions.IgnoreCase).Trim();
            if (!withoutNumberedVariant.Equals(withoutMagicPrefix, StringComparison.OrdinalIgnoreCase)) {
                yield return withoutNumberedVariant;
                yield return withoutNumberedVariant + " [1]";
            }
            string withoutUnpowered = Regex.Replace(withoutMagicPrefix, @"\s+\(Unpowered\)$", "", RegexOptions.IgnoreCase).Trim();
            if (!withoutUnpowered.Equals(withoutMagicPrefix, StringComparison.OrdinalIgnoreCase)) yield return withoutUnpowered;
            if (withoutMagicPrefix.Equals("Note: The Preceptor's Secrets", StringComparison.OrdinalIgnoreCase)) yield return "Note: The Preceptor's Secret";
            if (withoutMagicPrefix.Equals("Note: Walking Mausoleum", StringComparison.OrdinalIgnoreCase)) yield return "Note_ Walking Mausoleum _ Hidden Cave _ Gateway _ The Lord of Frenzied Flame";
            if (withoutMagicPrefix.StartsWith("Note:", StringComparison.OrdinalIgnoreCase)) yield return "Note: Gateway";
            if (withoutMagicPrefix.Contains("Bell Bearing", StringComparison.OrdinalIgnoreCase)) {
                string bell = withoutMagicPrefix;
                if (bell.Contains("Bernahl", StringComparison.OrdinalIgnoreCase)) yield return "Bernahl_s _ Rogier_s _ Iji_s Bell Bearing";
                if (bell.Contains("Kale", StringComparison.OrdinalIgnoreCase) || bell.Contains("Kalé", StringComparison.OrdinalIgnoreCase) || bell.Contains("Gostoc", StringComparison.OrdinalIgnoreCase) || bell.Contains("Blackguard", StringComparison.OrdinalIgnoreCase) || bell.Contains("Pidia", StringComparison.OrdinalIgnoreCase) || bell.Contains("Patches", StringComparison.OrdinalIgnoreCase)) yield return "Patches_ _ Kale_s _ Gostoc_s _ Blackguard_s _ Pidia_s Bell Bearing";
                if (bell.Contains("Gowry", StringComparison.OrdinalIgnoreCase) || bell.Contains("Sellen", StringComparison.OrdinalIgnoreCase) || bell.Contains("Thops", StringComparison.OrdinalIgnoreCase)) yield return "Gowry_s _ Sellen_s _ Thops_s Bell Bearing";
                if (bell.Contains("Miriel", StringComparison.OrdinalIgnoreCase) || bell.Contains("Corhyn", StringComparison.OrdinalIgnoreCase)) yield return "Miriel_s _ Corhyn_s _ D_s Bell Bearing";
                yield return "General Bell Bearing";
            }
            Match ammo = Regex.Match(withoutMagicPrefix, @"^(Great Arrow|Greatbolt|Arrow|Bolt|Ballista Bolt)\s*-\s*(.+?)(\s*\(Fletched\))?$", RegexOptions.IgnoreCase);
            if (!ammo.Success) yield break;
            string type = ammo.Groups[1].Value.Trim();
            string flavor = ammo.Groups[2].Value.Trim();
            string fletched = ammo.Groups[3].Success? " (Fletched)" : "";
            if (flavor.Equals(type, StringComparison.OrdinalIgnoreCase)) {
                yield return type + fletched;
            } else {
                yield return flavor + " " + type + fletched;
                yield return flavor + fletched;
            }
        }
        private string GetInventoryCatalogIconCachePath(InventoryCatalogItem item, string resolvedName) {
            string folder = Path.Combine(settingsFolder, "InventoryIconsGameV2HighRes");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, SanitizeFileName(item.Category + "_" + item.ParamId + "_" + resolvedName) + ".png");
        }
        private static void SetInventoryImageSource(Image target, string path) {
            target.Opacity = 1.0;
            RenderOptions.SetBitmapScalingMode(target, BitmapScalingMode.HighQuality);
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            target.Source = bmp;
        }
        private async System.Threading.Tasks.Task LoadInventoryCatalogIconAsync(InventoryCatalogItem item, Image target) {
            try {
                EldenRingItem? exactPart = itemDatabase.FirstOrDefault(x => (item.Category == "Weapons"? x.Category == "Weapon" : item.Category == "Armor"? x.Category == "Armor" : false) && x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                if (exactPart != null) {
                    string exactPath = GetDatabaseGameItemIconCachePath(exactPart);
                    if (!File.Exists(exactPath)) exactPath = await EnsureDatabaseItemIconCachedAsync(exactPart) ?? "";
                    if (!string.IsNullOrWhiteSpace(exactPath) && File.Exists(exactPath)) {
                        SetInventoryImageSource(target, exactPath);
                        return;
                    }
                }
                List<string> iconCandidates = GetInventoryIconNameCandidates(item.Name).ToList();
                if (item.Category == "Sorceries" || item.Category == "Incantations") {
                    string primaryPrefix = item.Category == "Sorceries"? "[Sorcery] " : "[Incantation] ";
                    string fallbackPrefix = item.Category == "Sorceries"? "[Incantation] " : "[Sorcery] ";
                    foreach (string baseName in iconCandidates.ToList()) {
                        if (!baseName.StartsWith("[Sorcery]", StringComparison.OrdinalIgnoreCase) &&!baseName.StartsWith("[Incantation]", StringComparison.OrdinalIgnoreCase)) {
                            iconCandidates.Add(primaryPrefix + baseName);
                            iconCandidates.Add(fallbackPrefix + baseName);
                        }
                    }
                }
                if (item.Category == "Spirit Ashes") {
                    foreach (string baseName in iconCandidates.ToList()) if (!baseName.EndsWith("Ashes", StringComparison.OrdinalIgnoreCase)) iconCandidates.Add(baseName + " Ashes");
                }
                foreach (string candidate in iconCandidates.Distinct(StringComparer.OrdinalIgnoreCase)) {
                    string cachePath = GetInventoryCatalogIconCachePath(item, candidate);
                    if (File.Exists(cachePath)) {
                        SetInventoryImageSource(target, cachePath);
                        return;
                    }
                    if (item.Category == "Weapons" && HasValidGameDataDirectory()) {
                        EldenRingItem synthetic = new EldenRingItem {
                            Name = candidate, Category = "Weapon", Type = "Weapon", ModelId = "", FileName = "", IsDlc = false
                        };
                        bool created = await EldenRingGameIconService.TryCreateHighResolutionIconAsync(gameDataFolder, synthetic, cachePath);
                        if (created && File.Exists(cachePath)) {
                            target.Opacity = 1.0;
                            RenderOptions.SetBitmapScalingMode(target, BitmapScalingMode.HighQuality);
                            SetInventoryImageSource(target, cachePath);
                            return;
                        }
                    }
                    if (LocalItemIconService.TryMaterializeIcon(candidate, cachePath) && File.Exists(cachePath)) {
                        target.Opacity = 1.0;
                        RenderOptions.SetBitmapScalingMode(target, BitmapScalingMode.HighQuality);
                        SetInventoryImageSource(target, cachePath);
                        return;
                    }
                }
            } catch {
            }
        }
        private InventorySnapshot ReadInventorySnapshot(string savePath, int slotIndex) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            return ParseInventorySnapshot(data, slotIndex);
        }
        private InventorySnapshot ParseInventorySnapshot(byte[] data, int slotIndex) {
            int slotStart = GetCharacterSlotDataOffset(slotIndex);
            int slotEnd = slotStart + EldenRingSlotDataSize;
            if (slotEnd> data.Length) throw new InvalidDataException("Character slot extends outside the save file.");
            uint slotVersion = BitConverter.ToUInt32(data, slotStart);
            int gaItemCount = slotVersion <= 81? 5118 : 5120;
            int pos = slotStart + 0x20;
            List<GaItemInfo> gaItems = new List<GaItemInfo>(gaItemCount);
            Dictionary<uint, GaItemInfo> byHandle = new Dictionary<uint, GaItemInfo>();
            for (int i = 0; i<gaItemCount; i++) {
                if (pos + 8> slotEnd) throw new InvalidDataException("GaItem table ended unexpectedly.");
                uint handle = BitConverter.ToUInt32(data, pos);
                uint itemId = BitConverter.ToUInt32(data, pos + 4);
                uint handleType = handle & 0xF0000000u;
                int size = 8;
                if (handle != 0u && handleType != 0xC0000000u) size += 8;
                if (handleType == 0x80000000u) size += 5;
                if (pos + size> slotEnd) throw new InvalidDataException("GaItem record extends outside the character slot.");
                uint gemHandle = 0u;
                if (handleType == 0x80000000u && size == 21) gemHandle = BitConverter.ToUInt32(data, pos + 16);
                GaItemInfo info = new GaItemInfo {
                    TableIndex = i, Offset = pos, Size = size, Handle = handle, ItemId = itemId, GemHandle = gemHandle
                };
                gaItems.Add(info);
                if (handle != 0 &&!byHandle.ContainsKey(handle)) byHandle[handle] = info;
                pos += size;
            }
            int playerStart = pos;
            int profileOffset = EldenRingUserData10DataOffset + EldenRingProfilesRelativeOffset + slotIndex * EldenRingProfileSize;
            string expectedName = ReadFixedUtf16String(data, profileOffset, 32);
            uint expectedLevel = BitConverter.ToUInt32(data, profileOffset + 0x22);
            if (playerStart<slotStart + 0x20 || playerStart + 0x1B0> slotEnd) throw new InvalidDataException("PlayerGameData boundary is outside the character slot.");
            uint parsedLevel = BitConverter.ToUInt32(data, playerStart + 0x60);
            string parsedName = ReadFixedUtf16String(data, playerStart + 0x94, 16);
            if (parsedLevel != expectedLevel || (!string.IsNullOrWhiteSpace(expectedName) &&!parsedName.Equals(expectedName, StringComparison.Ordinal))) {
                throw new InvalidDataException($"GaItem map did not end at PlayerGameData " + $"(0x{playerStart - slotStart:X}, '{parsedName}', Lv.{parsedLevel}).");
            }
            int heldOffset = playerStart + 0x3A4;
            int projectileCountOffset = heldOffset + 0x9010 + 0x74 + 0x8C + 0x18;
            if (projectileCountOffset + 4> slotEnd) throw new InvalidDataException("Held inventory layout is outside the character slot.");
            uint projectileCount = BitConverter.ToUInt32(data, projectileCountOffset);
            long afterProjectiles = (long) projectileCountOffset + 4L + (long) projectileCount * 8L;
            if (afterProjectiles<projectileCountOffset || afterProjectiles + 0x9C + 0x0C + 0x12F> slotEnd) throw new InvalidDataException("Acquired projectile list extends outside the character slot.");
            int storageOffset = checked ((int) afterProjectiles + 0x9C + 0x0C + 0x12F);
            if (storageOffset + 0x6010> slotEnd) throw new InvalidDataException("Storage inventory layout is outside the character slot.");
            InventorySnapshot snapshot = new InventorySnapshot {
                SlotIndex = slotIndex, SlotStart = slotStart, SlotEnd = slotEnd, GaItemsStart = slotStart + 0x20, GaItemsEnd = playerStart, HeldOffset = heldOffset, StorageOffset = storageOffset, GaItems = gaItems, GaItemByHandle = byHandle
            };
            ReadInventoryBlock(data, snapshot, heldOffset, 0xA80, 0x180, "Held");
            ReadInventoryBlock(data, snapshot, storageOffset, 0x780, 0x80, "Storage");
            return snapshot;
        }
        private void ReadInventoryBlock(byte[] data, InventorySnapshot snapshot, int baseOffset, int commonCapacity, int keyCapacity, string location) {
            uint commonCount = BitConverter.ToUInt32(data, baseOffset);
            if (commonCount> commonCapacity) throw new InvalidDataException(location + " common inventory count exceeds capacity.");
            int commonStart = baseOffset + 4;
            for (int i = 0; i<commonCount; i++) snapshot.Items.Add(ReadInventoryEntry(data, snapshot, commonStart + i * 12, location, false, i));
            int keyCountOffset = commonStart + commonCapacity * 12;
            uint keyCount = BitConverter.ToUInt32(data, keyCountOffset);
            if (keyCount> keyCapacity) throw new InvalidDataException(location + " key-item inventory count exceeds capacity.");
            int keyStart = keyCountOffset + 4;
            for (int i = 0; i<keyCount; i++) snapshot.Items.Add(ReadInventoryEntry(data, snapshot, keyStart + i * 12, location, true, i));
        }
        private InventoryEntryInfo ReadInventoryEntry(byte[] data, InventorySnapshot snapshot, int offset, string location, bool keyArray, int arrayIndex) {
            uint handle = BitConverter.ToUInt32(data, offset);
            uint quantity = BitConverter.ToUInt32(data, offset + 4);
            uint index = BitConverter.ToUInt32(data, offset + 8);
            snapshot.GaItemByHandle.TryGetValue(handle, out GaItemInfo? ga);
            uint handleType = handle & 0xF0000000u;
            uint lowHandleId = handle & 0x0FFFFFFFu;
            uint itemId = ga != null? ga.ItemId : handleType switch {
                0xA0000000u => 0x20000000u | lowHandleId, 0xB0000000u => 0x40000000u | lowHandleId, 0xC0000000u => 0x80000000u | lowHandleId, _ => 0u
            };
            string category = GetInventoryCategoryFromHandle(handle, keyArray);
            int upgrade = 0;
            int affinity = 0;
            bool supportsAffinity = false;
            int maxUpgrade = 0;
            uint paramId = DecodeInventoryParamId(itemId, category);
            uint ashOfWarHandle = 0u;
            uint ashOfWarParamId = 0u;
            if (category == "Weapons") {
                upgrade = (int)(paramId % 100u);
                uint withoutUpgrade = paramId - (uint) upgrade;
                uint baseId = (withoutUpgrade / 10000u) * 10000u;
                affinity = (int)((withoutUpgrade - baseId) / 100u);
                supportsAffinity = affinity> 0;
                maxUpgrade = 25;
                if (ga != null && ga.GemHandle != 0u && ga.GemHandle != 0xFFFFFFFFu) {
                    ashOfWarHandle = ga.GemHandle;
                    if (snapshot.GaItemByHandle.TryGetValue(ashOfWarHandle, out GaItemInfo? gemGa)) ashOfWarParamId = gemGa.ItemId & 0x0FFFFFFFu;
                }
            }
            return new InventoryEntryInfo {
                Offset = offset, ArrayIndex = arrayIndex, IsKeyArray = keyArray, Location = location, Handle = handle, Quantity = quantity, Index = index, ItemId = itemId, ParamId = paramId, Category = category, Name = $"Unknown {category} [{paramId}]", GaItem = ga, Upgrade = upgrade, Affinity = affinity, SupportsAffinity = supportsAffinity, AshOfWarHandle = ashOfWarHandle, AshOfWarParamId = ashOfWarParamId, MaxUpgrade = maxUpgrade == 0? 25 : maxUpgrade
            };
        }
        private static string GetInventoryCategoryFromHandle(uint handle, bool keyArray) {
            uint p = handle & 0xF0000000u;
            if (p == 0x80000000u) return "Weapons";
            if (p == 0x90000000u) return "Armor";
            if (p == 0xA0000000u) return "Talismans";
            if (p == 0xB0000000u) return keyArray? "Key Items" : "Consumables";
            if (p == 0xC0000000u) return "Ashes of War";
            return keyArray? "Key Items" : "Consumables";
        }
        private static uint DecodeInventoryParamId(uint itemId, string category) {
            if (category == "Weapons") return itemId;
            return itemId & 0x0FFFFFFFu;
        }
        private static uint EncodeInventoryItemId(InventoryCatalogItem item, int upgrade, int affinity) {
            if (item.Category == "Weapons") {
                uint baseId = item.BaseParamId != 0? item.BaseParamId : (item.ParamId / 10000u) * 10000u;
                return checked (baseId + (uint) Math.Max(0, affinity) * 100u + (uint) Math.Max(0, upgrade));
            }
            if (item.Category == "Armor") return 0x10000000u | item.ParamId;
            if (item.Category == "Talismans") return 0x20000000u | item.ParamId;
            if (item.Category == "Ashes of War") return 0x80000000u | item.ParamId;
            return 0x40000000u | item.ParamId;
        }
        private static string GetInventoryEntryDisplayName(InventoryEntryInfo entry) {
            if (entry.Category != "Weapons" || entry.Affinity <= 0) return entry.Name;
            string[] affinities = GetInventoryAffinityNames();
            if (entry.Affinity >= affinities.Length) return entry.Name;
            string prefix = affinities[entry.Affinity];
            if (entry.Name.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)) return entry.Name;
            return prefix + " " + entry.Name;
        }
        private InventoryCatalogItem? ResolveInventoryCatalogItem(InventoryEntryInfo entry, List<InventoryCatalogItem> catalog) {
            uint id = entry.Category == "Weapons"?(entry.ParamId / 10000u) * 10000u : entry.ParamId;
            InventoryCatalogItem? exact = catalog.FirstOrDefault(x => x.Category == entry.Category && x.ParamId == id);
            if (exact != null) return exact;
            if ((entry.Handle & 0xF0000000u) == 0xB0000000u || IsGoodsInventoryCategory(entry.Category)) {
                if (entry.IsKeyArray) {
                    InventoryCatalogItem? key = catalog.FirstOrDefault(x => x.Category == "Key Items" && x.ParamId == id);
                    if (key != null) return key;
                }
                InventoryCatalogItem? goods = catalog.FirstOrDefault(x => IsGoodsInventoryCategory(x.Category) && x.ParamId == id);
                if (goods != null) return goods;
                uint ashUpgrade = id % 100u;
                if (ashUpgrade> 0u && ashUpgrade <= 10u) {
                    uint ashBaseId = id - ashUpgrade;
                    InventoryCatalogItem? spiritAsh = catalog.FirstOrDefault(x => x.Category == "Spirit Ashes" && x.ParamId == ashBaseId);
                    if (spiritAsh != null) return spiritAsh;
                }
            }
            return null;
        }
        private void ApplyCatalogNames(InventorySnapshot snapshot, List<InventoryCatalogItem> catalog) {
            foreach (InventoryEntryInfo entry in snapshot.Items) {
                InventoryCatalogItem? item = ResolveInventoryCatalogItem(entry, catalog);
                if (item != null) {
                    entry.Category = entry.IsKeyArray && (entry.Handle & 0xF0000000u) == 0xB0000000u? "Key Items" : item.Category;
                    entry.Name = item.Name;
                    entry.SupportsAffinity = item.SupportsAffinity;
                    entry.SupportsAshOfWar = item.SupportsAshOfWar;
                    entry.MaxUpgrade = item.MaxUpgrade;
                    if (entry.Category == "Spirit Ashes" && entry.ParamId >= item.ParamId) entry.Upgrade = (int) Math.Min(10u, entry.ParamId - item.ParamId);
                }
                if (entry.Category == "Weapons" && entry.AshOfWarParamId != 0u) {
                    InventoryCatalogItem? ash = catalog.FirstOrDefault(x => x.Category == "Ashes of War" && x.ParamId == entry.AshOfWarParamId);
                    entry.AshOfWarName = ash?.Name ?? $"Unknown AoW [{entry.AshOfWarParamId}]";
                } else if (entry.Category == "Weapons") {
                    entry.AshOfWarName = "None";
                }
            }
        }
        private InventoryCatalogItem? FindCatalogItemForEntry(InventoryEntryInfo entry, List<InventoryCatalogItem> catalog) {
            return ResolveInventoryCatalogItem(entry, catalog);
        }
        private uint AddInventoryCatalogItem(string savePath, int slotIndex, InventoryCatalogItem item, uint quantity, int upgrade, int affinity, string location) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            InventorySnapshot snapshot = ParseInventorySnapshot(data, slotIndex);
            uint itemId = EncodeInventoryItemId(item, upgrade, affinity);
            bool keyArray = item.Category == "Key Items";
            InventoryEntryInfo? existingStack = snapshot.Items.FirstOrDefault(x => x.Location == location && x.IsKeyArray == keyArray && x.ItemId == itemId && IsGoodsInventoryCategory(item.Category));
            if (existingStack != null) {
                uint newQty = Math.Min(9999u, checked (existingStack.Quantity + quantity));
                CreateEditorSafetyBackupOnce();
                WriteUInt32(data, existingStack.Offset + 4, newQty);
                RecalculateSlotChecksum(data, slotIndex);
                WriteSaveAtomically(savePath, data);
                return existingStack.Handle;
            }
            uint newHandle;
            bool directHandle = item.Category == "Talismans" || IsGoodsInventoryCategory(item.Category);
            if (directHandle) {
                uint prefix = item.Category == "Talismans"? 0xA0000000u : 0xB0000000u;
                newHandle = prefix | (item.ParamId & 0x0FFFFFFFu);
            } else if (item.Category == "Ashes of War") {
                GaItemInfo? empty = snapshot.GaItems.FirstOrDefault(x => x.IsEmpty && x.Size == 8);
                if (empty == null) throw new InvalidOperationException("No free 8-byte GaItem slot is available for an Ash of War.");
                uint maxLow = snapshot.GaItems.Where(x => (x.Handle & 0xF0000000u) == 0xC0000000u).Select(x => x.Handle & 0x0FFFFFFFu).DefaultIfEmpty(0x00800000u).Max();
                newHandle = 0xC0000000u | ((maxLow + 1u) & 0x0FFFFFFFu);
                if ((newHandle & 0x0FFFFFFFu) == 0u) throw new InvalidOperationException("Could not allocate a safe Ash of War handle.");
                WriteUInt32(data, empty.Offset, newHandle);
                WriteUInt32(data, empty.Offset + 4, itemId);
            } else {
                int gaSize = item.Category == "Weapons"? 21 : 16;
                int emptyIndex = snapshot.GaItems.FindIndex(x => x.IsEmpty);
                if (emptyIndex<0) throw new InvalidOperationException("No free GaItem record is available in this character slot.");
                GaItemInfo empty = snapshot.GaItems[emptyIndex];
                int delta = gaSize - 8;
                if (delta> 0) {
                    for (int i = 0; i<delta; i++) if (data[snapshot.SlotEnd - 1 - i] != 0) throw new InvalidDataException("The character slot has no safe zero padding available for a new equipment record.");
                    Buffer.BlockCopy(data, empty.Offset + 8, data, empty.Offset + gaSize, snapshot.SlotEnd - delta - (empty.Offset + 8));
                    Array.Clear(data, snapshot.SlotEnd - delta, delta);
                }
                uint handlePrefix = item.Category == "Weapons"? 0x80000000u : 0x90000000u;
                uint maxLow = snapshot.GaItems.Where(x => (x.Handle & 0xF0000000u) == handlePrefix).Select(x => x.Handle & 0x0FFFFFFFu).DefaultIfEmpty(0x00010000u).Max();
                newHandle = handlePrefix | ((maxLow + 1u) & 0x0FFFFFFFu);
                if ((newHandle & 0x0FFFFFFFu) == 0) throw new InvalidOperationException("Could not allocate a safe GaItem handle.");
                WriteUInt32(data, empty.Offset, newHandle);
                WriteUInt32(data, empty.Offset + 4, itemId);
                WriteUInt32(data, empty.Offset + 8, 0xFFFFFFFFu);
                WriteUInt32(data, empty.Offset + 12, 0xFFFFFFFFu);
                if (gaSize == 21) {
                    WriteUInt32(data, empty.Offset + 16, 0xFFFFFFFFu);
                    data[empty.Offset + 20] = 0;
                }
            }
            if (item.Category == "Weapons" || item.Category == "Armor") snapshot = ParseInventorySnapshot(data, slotIndex);
            int blockOffset = location == "Storage"? snapshot.StorageOffset : snapshot.HeldOffset;
            int commonCap = location == "Storage"? 0x780 : 0xA80;
            int keyCap = location == "Storage"? 0x80 : 0x180;
            int commonStart = blockOffset + 4;
            int keyCountOffset = commonStart + commonCap * 12;
            int keyStart = keyCountOffset + 4;
            int nextIndexOffset = keyStart + keyCap * 12;
            int countOffset = keyArray? keyCountOffset : blockOffset;
            int start = keyArray? keyStart : commonStart;
            int cap = keyArray? keyCap : commonCap;
            uint count = BitConverter.ToUInt32(data, countOffset);
            if (count >= cap) throw new InvalidOperationException(location + " inventory is full for this item category.");
            int entryOffset = start + checked ((int) count * 12);
            uint nextIndex = BitConverter.ToUInt32(data, nextIndexOffset);
            if (nextIndex <= 432) nextIndex = 433;
            WriteUInt32(data, entryOffset, newHandle);
            uint storedQuantity = (item.Category == "Weapons" || item.Category == "Armor" || item.Category == "Talismans" || item.Category == "Ashes of War")? 1u : quantity;
            WriteUInt32(data, entryOffset + 4, storedQuantity);
            WriteUInt32(data, entryOffset + 8, nextIndex);
            WriteUInt32(data, countOffset, count + 1);
            WriteUInt32(data, nextIndexOffset, nextIndex + 1);
            uint nextSort = BitConverter.ToUInt32(data, nextIndexOffset + 4);
            WriteUInt32(data, nextIndexOffset + 4, nextSort + 1);
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            WriteSaveAtomically(savePath, data);
            return newHandle;
        }
        private void UpdateInventoryWeaponAshOfWar(string savePath, int slotIndex, InventoryEntryInfo entry, InventoryCatalogItem? ashOfWar) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            InventorySnapshot snapshot = ParseInventorySnapshot(data, slotIndex);
            InventoryEntryInfo? current = snapshot.Items.FirstOrDefault(x => x.Location == entry.Location && x.Handle == entry.Handle && x.Index == entry.Index);
            if (current?.GaItem == null || current.Category != "Weapons" || current.GaItem.Size != 21) throw new InvalidOperationException("The selected weapon GaItem could not be located.");
            uint oldGemHandle = current.GaItem.GemHandle;
            uint newGemHandle = 0u;
            if (ashOfWar != null) {
                if (ashOfWar.Category != "Ashes of War") throw new InvalidOperationException("The selected item is not an Ash of War.");
                GaItemInfo? gem = null;
                if (oldGemHandle != 0u && oldGemHandle != 0xFFFFFFFFu) snapshot.GaItemByHandle.TryGetValue(oldGemHandle, out gem);
                if (gem != null && (gem.Handle & 0xF0000000u) == 0xC0000000u && gem.Size == 8) {
                    WriteUInt32(data, gem.Offset + 4, 0x80000000u | (ashOfWar.ParamId & 0x0FFFFFFFu));
                    newGemHandle = gem.Handle;
                } else {
                    GaItemInfo? empty = snapshot.GaItems.FirstOrDefault(x => x.IsEmpty && x.Size == 8);
                    if (empty == null) throw new InvalidOperationException("No free 8-byte GaItem slot is available for an Ash of War.");
                    uint maxLow = snapshot.GaItems.Where(x => (x.Handle & 0xF0000000u) == 0xC0000000u).Select(x => x.Handle & 0x0FFFFFFFu).DefaultIfEmpty(0x00800000u).Max();
                    newGemHandle = 0xC0000000u | ((maxLow + 1u) & 0x0FFFFFFFu);
                    if ((newGemHandle & 0x0FFFFFFFu) == 0u) throw new InvalidOperationException("Could not allocate a safe Ash of War handle.");
                    WriteUInt32(data, empty.Offset, newGemHandle);
                    WriteUInt32(data, empty.Offset + 4, 0x80000000u | (ashOfWar.ParamId & 0x0FFFFFFFu));
                }
            }
            WriteUInt32(data, current.GaItem.Offset + 16, newGemHandle);
            if (ashOfWar == null && oldGemHandle != 0u && oldGemHandle != 0xFFFFFFFFu && snapshot.GaItemByHandle.TryGetValue(oldGemHandle, out GaItemInfo? oldGem) && (oldGem.Handle & 0xF0000000u) == 0xC0000000u && oldGem.Size == 8) {
                bool referencedElsewhere = snapshot.GaItems.Any(x => x.Handle != current.GaItem.Handle && x.Size == 21 && x.GemHandle == oldGemHandle);
                if (!referencedElsewhere) {
                    WriteUInt32(data, oldGem.Offset, 0u);
                    WriteUInt32(data, oldGem.Offset + 4, 0u);
                }
            }
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            WriteSaveAtomically(savePath, data);
        }
        private void RemoveInventoryEntry(string savePath, int slotIndex, InventoryEntryInfo entry) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            InventorySnapshot snapshot = ParseInventorySnapshot(data, slotIndex);
            InventoryEntryInfo? current = snapshot.Items.FirstOrDefault(x => x.Location == entry.Location && x.IsKeyArray == entry.IsKeyArray && x.Handle == entry.Handle && x.Index == entry.Index);
            if (current == null) throw new InvalidOperationException("The selected inventory entry changed; reopen Edit Inventory.");
            int blockOffset = current.Location == "Storage"? snapshot.StorageOffset : snapshot.HeldOffset;
            int commonCap = current.Location == "Storage"? 0x780 : 0xA80;
            int keyCap = current.Location == "Storage"? 0x80 : 0x180;
            int commonStart = blockOffset + 4;
            int keyCountOffset = commonStart + commonCap * 12;
            int keyStart = keyCountOffset + 4;
            int countOffset = current.IsKeyArray? keyCountOffset : blockOffset;
            int start = current.IsKeyArray? keyStart : commonStart;
            uint count = BitConverter.ToUInt32(data, countOffset);
            int index = current.ArrayIndex;
            if (index<0 || index >= count) throw new InvalidDataException("Inventory array index is invalid.");
            int moveEntries = (int) count - index - 1;
            if (moveEntries> 0) Buffer.BlockCopy(data, start + (index + 1) * 12, data, start + index * 12, moveEntries * 12);
            Array.Clear(data, start + ((int) count - 1) * 12, 12);
            WriteUInt32(data, countOffset, count - 1);
            CreateEditorSafetyBackupOnce();
            RecalculateSlotChecksum(data, slotIndex);
            WriteSaveAtomically(savePath, data);
        }
        private void UpdateInventoryQuantity(string savePath, int slotIndex, InventoryEntryInfo entry, uint quantity) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            InventorySnapshot snapshot = ParseInventorySnapshot(data, slotIndex);
            InventoryEntryInfo? current = snapshot.Items.FirstOrDefault(x => x.Location == entry.Location && x.Handle == entry.Handle && x.Index == entry.Index);
            if (current == null) throw new InvalidOperationException("The selected inventory entry changed; reopen Edit Inventory.");
            CreateEditorSafetyBackupOnce();
            WriteUInt32(data, current.Offset + 4, quantity);
            RecalculateSlotChecksum(data, slotIndex);
            WriteSaveAtomically(savePath, data);
        }
        private bool ValidateInventoryWeaponUpgrade(string weaponName, int requestedUpgrade, int maxUpgrade) {
            int safeMax = Math.Max(0, maxUpgrade);
            if (requestedUpgrade<0 || requestedUpgrade> safeMax) {
                string message = safeMax == 0?$"{weaponName} cannot be reinforced" : safeMax <= 10?$"{weaponName} can only be reinforced to +{safeMax} • Somber weapon" : $"{weaponName} can only be reinforced to +{safeMax}";
                ShowToast(message, ToastKind.Warning);
                return false;
            }
            return true;
        }
        private void UpdateInventoryWeaponVariant(string savePath, int slotIndex, InventoryEntryInfo entry, int upgrade, int affinity) {
            byte[] data = File.ReadAllBytes(savePath);
            ValidatePcSaveData(data);
            InventorySnapshot snapshot = ParseInventorySnapshot(data, slotIndex);
            InventoryEntryInfo? current = snapshot.Items.FirstOrDefault(x => x.Location == entry.Location && x.Handle == entry.Handle && x.Index == entry.Index);
            if (current?.GaItem == null || current.Category != "Weapons") throw new InvalidOperationException("The selected weapon GaItem could not be located.");
            uint baseId = (current.ParamId / 10000u) * 10000u;
            uint itemId = checked (baseId + (uint) Math.Max(0, affinity) * 100u + (uint) Math.Max(0, upgrade));
            CreateEditorSafetyBackupOnce();
            WriteUInt32(data, current.GaItem.Offset + 4, itemId);
            RecalculateSlotChecksum(data, slotIndex);
            WriteSaveAtomically(savePath, data);
        }
        private void ShowGenericPage(string title, string subtitle) {
            ContentHost.Children.Clear();
            StackPanel stack = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(new TextBlock {
                Text = title, Foreground = normalTextBrush, FontFamily = new FontFamily("Georgia"), FontSize = 28, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock {
                Text = subtitle, Foreground = mutedTextBrush, FontSize = 14, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center
            });
            ContentHost.Children.Add(stack);
        }
        private void BuildItemReplacePage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Item Replace", "Import a skin mod first, choose a compatible replacement target, check the active Mod Engine 2 folder, then install."));
            Border sourceCard = CreateCard();
            sourceCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel sourceStack = new StackPanel();
            sourceStack.Children.Add(CreateStepHeader("1", "NEW MOD SOURCE", "Choose a newly downloaded mod folder, or select one or more individual .partsbnd.dcx files."));
            Grid sourceGrid = new Grid {
                Margin = new Thickness(0, 12, 0, 0)
            };
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            modFolderTextBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Text = itemReplaceSourceDisplay, IsReadOnly = true
            };
            sourceGrid.Children.Add(modFolderTextBox);
            Button browseFolderButton = new Button {
                Content = "Browse Folder...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(10, 0, 0, 0)
            };
            browseFolderButton.Click += BrowseModFolder_Click;
            Grid.SetColumn(browseFolderButton, 1);
            sourceGrid.Children.Add(browseFolderButton);
            Button browseFileButton = new Button {
                Content = "Browse File...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            browseFileButton.Click += BrowseModFile_Click;
            Grid.SetColumn(browseFileButton, 2);
            sourceGrid.Children.Add(browseFileButton);
            sourceStack.Children.Add(sourceGrid);
            modFolderInfoText = CreateInfoLine("Source", string.IsNullOrWhiteSpace(itemReplaceSourceInfo)? "None" : itemReplaceSourceInfo.Replace("Source: ", ""));
            sourceStack.Children.Add(modFolderInfoText);
            detectedModTypeText = CreateInfoLine("Detected Types", "None");
            detectedModItemText = CreateInfoLine("Detected Items", "None");
            detectedModFileText = CreateInfoLine("Detected Files", "None");
            detectedModItemText.TextWrapping = TextWrapping.Wrap;
            detectedModFileText.TextWrapping = TextWrapping.Wrap;
            sourceStack.Children.Add(detectedModTypeText);
            sourceStack.Children.Add(detectedModItemText);
            sourceStack.Children.Add(detectedModFileText);
            sourceCard.Child = sourceStack;
            root.Children.Add(sourceCard);
            replacementTargetCard = CreateCard();
            replacementTargetCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel targetStack = new StackPanel();
            targetStack.Children.Add(CreateStepHeader("2", "REPLACEMENT TARGET", "Choose the in-game item slot that this skin mod should replace. Only compatible target types are shown."));
            Grid targetPickerRow = new Grid {
                Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed
            };
            targetPickerRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            targetPickerRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            targetItemComboBox = new ComboBox {
                Style = (Style) FindResource("DarkEditableComboBox"), IsEditable = true, IsTextSearchEnabled = false, StaysOpenOnEdit = true
            };
            targetItemComboBox.DisplayMemberPath = "Name";
            TextSearch.SetTextPath(targetItemComboBox, "Name");
            targetItemComboBox.SelectionChanged += TargetItemComboBox_SelectionChanged;
            targetItemComboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(TargetItemComboBox_TextChanged));
            targetItemComboBox.PreviewKeyDown += TargetItemComboBox_PreviewKeyDown;
            Button visualTargetButton = new Button {
                Content = "Visual Select...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0), MinWidth = 118
            };
            visualTargetButton.Click += (_, _) => {
                OpenVisualTargetPicker();
            };
            Grid.SetColumn(targetItemComboBox, 0);
            Grid.SetColumn(visualTargetButton, 1);
            targetPickerRow.Children.Add(targetItemComboBox);
            targetPickerRow.Children.Add(visualTargetButton);
            targetFilterHintText = CreateFieldLabel("Import a mod source first.");
            targetFilterHintText.Margin = new Thickness(0, 9, 0, 0);
            targetStack.Children.Add(targetPickerRow);
            targetStack.Children.Add(targetFilterHintText);
            targetPieceOverridePanel = new StackPanel {
                Visibility = Visibility.Collapsed
            };
            targetStack.Children.Add(targetPieceOverridePanel);
            selectedItemNameText = CreateInfoLine("Name", "-");
            selectedItemTypeText = CreateInfoLine("Type", "-");
            selectedItemIdText = CreateInfoLine("Model ID", "-");
            selectedItemFileText = CreateInfoLine("Target File", "-");
            targetStack.Children.Add(selectedItemNameText);
            targetStack.Children.Add(selectedItemTypeText);
            targetStack.Children.Add(selectedItemIdText);
            targetStack.Children.Add(selectedItemFileText);
            replacementTargetCard.Child = targetStack;
            root.Children.Add(replacementTargetCard);
            compatibilityCard = CreateCard();
            compatibilityCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel compatibilityStack = new StackPanel();
            compatibilityStack.Children.Add(CreateStepHeader("3", "COMPATIBILITY", "Checks whether the imported mod type can be remapped to the selected target."));
            compatibilityText = new TextBlock {
                Text = "Waiting for a mod source and target.", Foreground = mutedTextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 16)
            };
            compatibilityStack.Children.Add(compatibilityText);
            compatibilityCheckButton = new Button {
                Content = "Check Compatibility", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Right
            };
            compatibilityCheckButton.Click += CheckCompatibility_Click;
            compatibilityStack.Children.Add(compatibilityCheckButton);
            compatibilityCard.Child = compatibilityStack;
            root.Children.Add(compatibilityCard);
            conflictCard = CreateCard();
            conflictCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel conflictStack = new StackPanel();
            conflictStack.Children.Add(CreateStepHeader("4", "MOD FOLDER CONFLICTS", "Checks whether the selected target slot is already occupied in the active Mod Engine 2 folder."));
            conflictStatusText = new TextBlock {
                Text = "Compatibility must pass first.", Foreground = mutedTextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 16)
            };
            conflictStack.Children.Add(conflictStatusText);
            conflictActionPanel = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12)
            };
            Button overwriteButton = new Button {
                Content = "Overwrite Existing", Style = (Style) FindResource("ActionButton")
            };
            overwriteButton.Click += OverwriteExisting_Click;
            Button cancelConflictButton = new Button {
                Content = "Cancel", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            cancelConflictButton.Click += CancelConflict_Click;
            Button recommendButton = new Button {
                Content = "Use Recommended Slot", Style = (Style) FindResource("PrimaryButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            recommendButton.Click += UseRecommendedSlot_Click;
            conflictActionPanel.Children.Add(overwriteButton);
            conflictActionPanel.Children.Add(cancelConflictButton);
            conflictActionPanel.Children.Add(recommendButton);
            conflictStack.Children.Add(conflictActionPanel);
            conflictCheckButton = new Button {
                Content = "Check Mod Folder", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Right
            };
            conflictCheckButton.Click += CheckModFolderOnly_Click;
            conflictStack.Children.Add(conflictCheckButton);
            conflictCard.Child = conflictStack;
            root.Children.Add(conflictCard);
            installCard = CreateCard();
            StackPanel installStack = new StackPanel();
            installStack.Children.Add(CreateStepHeader("5", "INSTALL", "Copies the imported file(s) into the active Mod Engine 2 folder using the selected target model ID."));
            installSummaryText = CreateFieldLabel("Complete the checks above before installing.");
            installSummaryText.Margin = new Thickness(0, 10, 0, 16);
            installStack.Children.Add(installSummaryText);
            installButton = new Button {
                Content = "Install", Style = (Style) FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = false
            };
            installButton.Click += InstallReplace_Click;
            installStack.Children.Add(installButton);
            installCard.Child = installStack;
            root.Children.Add(installCard);
            ContentHost.Children.Add(root);
            RestoreItemReplaceUi();
            UpdateItemReplaceStepStates();
        }
        private StackPanel CreateStepHeader(string number, string title, string description) {
            StackPanel root = new StackPanel();
            root.Children.Add(new TextBlock {
                Text = "STEP " + number, Foreground = GetResourceBrush("AccentBright"), FontSize = 10, FontWeight = FontWeights.Bold
            });
            root.Children.Add(new TextBlock {
                Text = title, Foreground = GetResourceBrush("TextMuted"), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 0)
            });
            root.Children.Add(CreateFieldLabel(description));
            return root;
        }
        private void BuildItemDatabasePage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Item Database", "Search Elden Ring parts by name, weapon class, armor type, model ID or game filename."));
            Border searchCard = CreateCard();
            searchCard.Margin = new Thickness(0, 0, 0, 16);
            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(210)
            });
            databaseSearchBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Margin = new Thickness(0, 0, 10, 0)
            };
            databaseTypeFilter = new ComboBox {
                Style = (Style) FindResource("DarkComboBox")
            };
            databaseTypeFilter.Items.Add("All Types");
            foreach (string type in itemDatabase.Where(IsBrowsableItemDatabaseEntry).Select(x => x.Type).Where(x =>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetTypeSortOrder).ThenBy(x => x)) {
                databaseTypeFilter.Items.Add(type);
            }
            databaseTypeFilter.SelectedIndex = 0;
            databaseFilterTimer ??= new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            databaseFilterTimer.Tick -= DatabaseFilterTimer_Tick;
            databaseFilterTimer.Tick += DatabaseFilterTimer_Tick;
            databaseSearchBox.TextChanged += DatabaseSearchBox_TextChanged;
            databaseTypeFilter.SelectionChanged += DatabaseTypeFilter_SelectionChanged;
            searchGrid.Children.Add(databaseSearchBox);
            Grid.SetColumn(databaseTypeFilter, 1);
            searchGrid.Children.Add(databaseTypeFilter);
            searchCard.Child = searchGrid;
            root.Children.Add(searchCard);
            databaseCountText = new TextBlock {
                Foreground = mutedTextBrush, FontSize = 12, Margin = new Thickness(4, 0, 0, 10)
            };
            root.Children.Add(databaseCountText);
            Grid databaseGrid = new Grid();
            databaseGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(2, GridUnitType.Star)
            });
            databaseGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Border listCard = CreateCard();
            listCard.Margin = new Thickness(0, 0, 8, 0);
            StackPanel listRoot = new StackPanel();
            listRoot.Children.Add(CreateSectionTitle("ITEMS"));
            databaseResultsList = new ListBox {
                Height = 390, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 12, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, DisplayMemberPath = "Name"
            };
            VirtualizingPanel.SetIsVirtualizing(databaseResultsList, true);
            VirtualizingPanel.SetVirtualizationMode(databaseResultsList, VirtualizationMode.Recycling);
            ScrollViewer.SetCanContentScroll(databaseResultsList, true);
            ScrollViewer.SetVerticalScrollBarVisibility(databaseResultsList, ScrollBarVisibility.Auto);
            Style databaseItemStyle = new Style(typeof(ListBoxItem));
            databaseItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            databaseItemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
            databaseItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, normalTextBrush));
            databaseItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            databaseItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            databaseItemStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            databaseItemStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            itemBorder.SetValue(Border.PaddingProperty, new Thickness(12, 9, 12, 9));
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            contentPresenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            contentPresenter.SetBinding(ContentPresenter.ContentStringFormatProperty, new System.Windows.Data.Binding("ContentStringFormat") {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            itemBorder.AppendChild(contentPresenter);
            ControlTemplate databaseItemTemplate = new ControlTemplate(typeof(ListBoxItem)) {
                VisualTree = itemBorder
            };
            Trigger hoverTrigger = new Trigger {
                Property = ListBoxItem.IsMouseOverProperty, Value = true
            };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropHover")));
            databaseItemTemplate.Triggers.Add(hoverTrigger);
            Trigger selectedTrigger = new Trigger {
                Property = ListBoxItem.IsSelectedProperty, Value = true
            };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("DropSelected")));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, normalTextBrush));
            databaseItemTemplate.Triggers.Add(selectedTrigger);
            databaseItemStyle.Setters.Add(new Setter(Control.TemplateProperty, databaseItemTemplate));
            databaseResultsList.ItemContainerStyle = databaseItemStyle;
            databaseResultsList.SelectionChanged += DatabaseResultsList_SelectionChanged;
            listRoot.Children.Add(databaseResultsList);
            listCard.Child = listRoot;
            databaseGrid.Children.Add(listCard);
            Border detailsCard = CreateCard();
            detailsCard.Margin = new Thickness(8, 0, 0, 0);
            StackPanel detailsRoot = new StackPanel();
            detailsRoot.Children.Add(CreateSectionTitle("ITEM DETAILS"));
            Border imageFrame = new Border {
                Height = 176, Margin = new Thickness(0, 12, 0, 16), Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderSoft"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3)
            };
            Grid imageGrid = new Grid();
            databaseDetailsImage = new Image {
                Width = 128, Height = 128, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            databaseDetailsImageStatus = new TextBlock {
                Text = "Select an item", Foreground = GetResourceBrush("TextMuted"), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 0, 18, 0)
            };
            imageGrid.Children.Add(databaseDetailsImage);
            imageGrid.Children.Add(databaseDetailsImageStatus);
            imageFrame.Child = imageGrid;
            detailsRoot.Children.Add(imageFrame);
            databaseDetailsName = CreateInfoLine("Name", "-");
            databaseDetailsType = CreateInfoLine("Type", "-");
            databaseDetailsModel = CreateInfoLine("Model ID", "-");
            databaseDetailsFile = CreateInfoLine("File", "-");
            databaseDetailsGame = CreateInfoLine("Game", "-");
            detailsRoot.Children.Add(databaseDetailsName);
            detailsRoot.Children.Add(databaseDetailsType);
            detailsRoot.Children.Add(databaseDetailsModel);
            detailsRoot.Children.Add(databaseDetailsFile);
            detailsRoot.Children.Add(databaseDetailsGame);
            detailsCard.Child = detailsRoot;
            Grid.SetColumn(detailsCard, 1);
            databaseGrid.Children.Add(detailsCard);
            root.Children.Add(databaseGrid);
            ContentHost.Children.Add(root);
            RefreshDatabaseResults();
        }
        private void DatabaseFilterChanged(object?sender, EventArgs e) {
            RefreshDatabaseResults();
        }
        private void DatabaseSearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (databaseFilterTimer == null) {
                RefreshDatabaseResults();
                return;
            }
            databaseFilterTimer.Stop();
            databaseFilterTimer.Start();
        }
        private void DatabaseTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            databaseFilterTimer?.Stop();
            RefreshDatabaseResults();
        }
        private void DatabaseFilterTimer_Tick(object?sender, EventArgs e) {
            databaseFilterTimer?.Stop();
            RefreshDatabaseResults();
        }
        private void RefreshDatabaseResults() {
            if (databaseResultsList == null) {
                return;
            }
            string search = databaseSearchBox?.Text?.Trim() ?? "";
            string filter = databaseTypeFilter?.SelectedItem?.ToString() ?? "All Types";
            IEnumerable<EldenRingItem> query = itemDatabase.Where(IsBrowsableItemDatabaseEntry);
            if (!string.IsNullOrWhiteSpace(search)) {
                query = query.Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || item.ModelId.Contains(search, StringComparison.OrdinalIgnoreCase) || item.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Type.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            if (filter != "All Types") {
                query = query.Where(item => item.Type.Equals(filter, StringComparison.OrdinalIgnoreCase));
            }
            List<EldenRingItem> filtered = query.OrderBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => x.Name).ToList();
            if (databaseCountText != null) {
                databaseCountText.Text = filtered.Count + " item(s)";
            }
            databaseResultsList.ItemsSource = filtered;
            if (filtered.Count> 0) {
                databaseResultsList.SelectedIndex = 0;
                ShowDatabaseItemDetails(filtered[0]);
            } else {
                databaseResultsList.SelectedItem = null;
            }
        }
        private void DatabaseResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (databaseResultsList?.SelectedItem is EldenRingItem item) {
                ShowDatabaseItemDetails(item);
            }
        }
        private void DatabaseItem_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is EldenRingItem item) {
                ShowDatabaseItemDetails(item);
            }
        }
        private void ShowDatabaseItemDetails(EldenRingItem item) {
            if (databaseDetailsName == null) {
                return;
            }
            databaseDetailsName.Text = "Name: " + item.Name;
            databaseDetailsType!.Text = "Type: " + item.Type;
            databaseDetailsModel!.Text = "Model ID: " + item.ModelId;
            databaseDetailsFile!.Text = "File: " + item.FileName;
            databaseDetailsGame!.Text = "Game: " + item.DlcText;
            UpdateDatabaseItemImage(item);
        }
        private async void UpdateDatabaseItemImage(EldenRingItem item) {
            if (databaseDetailsImage == null || databaseDetailsImageStatus == null) return;
            databaseDetailsImage.Source = null;
            databaseDetailsImageStatus.Visibility = Visibility.Visible;
            databaseDetailsImageStatus.Text = "Loading item icon...";
            string gameCachePath = GetDatabaseGameItemIconCachePath(item);
            if (File.Exists(gameCachePath)) {
                LoadDatabaseIconFromFile(gameCachePath);
                return;
            }
            string?fallbackPath = await EnsureDatabaseItemIconCachedAsync(item);
            if (databaseResultsList?.SelectedItem is not EldenRingItem selected ||!selected.Name.Equals(item.Name, StringComparison.Ordinal)) return;
            if (!string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath)) {
                LoadDatabaseIconFromFile(fallbackPath);
            } else {
                databaseDetailsImageStatus.Text = "Icon unavailable\nNo matching local icon was found for this item.";
            }
            if (!HasValidGameDataDirectory()) return;
            if (fullIconPrecacheRunning) return;
            bool upgraded = await EldenRingGameIconService.TryCreateHighResolutionIconAsync(gameDataFolder, item, gameCachePath);
            if (!upgraded) return;
            if (databaseResultsList?.SelectedItem is EldenRingItem stillSelected && stillSelected.Name.Equals(item.Name, StringComparison.Ordinal)) {
                LoadDatabaseIconFromFile(gameCachePath);
            }
        }
        private bool HasConfiguredGameDataDirectory() {
            return!string.IsNullOrWhiteSpace(gameDataFolder);
        }
        private bool HasValidGameDataDirectory() {
            return HasConfiguredGameDataDirectory() && Directory.Exists(gameDataFolder) && File.Exists(Path.Combine(gameDataFolder, "eldenring.exe"));
        }
        private string GetDatabaseItemIconCachePath(EldenRingItem item) {
            string folder = Path.Combine(settingsFolder, "ItemIcons");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, SanitizeFileName(item.Type + "_" + item.ModelId + "_" + item.Name) + ".png");
        }
        private string GetDatabaseGameItemIconCachePath(EldenRingItem item) {
            string folder = Path.Combine(settingsFolder, "ItemIconsGameV7");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, SanitizeFileName(item.Type + "_" + item.ModelId + "_" + item.Name) + ".png");
        }
        private System.Threading.Tasks.Task<string?> EnsureDatabaseItemIconCachedAsync(EldenRingItem item) {
            string cachePath = GetDatabaseItemIconCachePath(item);
            if (File.Exists(cachePath)) {
                return System.Threading.Tasks.Task.FromResult<string?>(cachePath);
            }
            bool cached = LocalItemIconService.TryMaterializeIcon(item.Name, cachePath);
            return System.Threading.Tasks.Task.FromResult<string?>(cached && File.Exists(cachePath)? cachePath : null);
        }
        private void LoadDatabaseIconFromFile(string iconPath) {
            if (databaseDetailsImage == null || databaseDetailsImageStatus == null) return;
            try {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                bool isGameHighRes = iconPath.IndexOf(Path.DirectorySeparatorChar + "ItemIconsGameV7" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
                RenderOptions.SetBitmapScalingMode(databaseDetailsImage, isGameHighRes? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
                databaseDetailsImage.SnapsToDevicePixels = true;
                RenderOptions.SetEdgeMode(databaseDetailsImage, isGameHighRes? EdgeMode.Unspecified : EdgeMode.Aliased);
                databaseDetailsImage.Source = bitmap;
                databaseDetailsImageStatus.Visibility = Visibility.Collapsed;
            } catch {
                databaseDetailsImage.Source = null;
                databaseDetailsImageStatus.Visibility = Visibility.Visible;
                databaseDetailsImageStatus.Text = "Unable to load item icon";
            }
        }
        private async System.Threading.Tasks.Task<bool> TryCacheDatabaseItemIconAsync(EldenRingItem item, string outputPath) {
            string endpoint = GetFanApiEndpoint(item);
            if (string.IsNullOrWhiteSpace(endpoint)) return false;
            foreach (string lookupName in GetItemIconLookupNames(item.Name)) {
                string key = endpoint + "|" + NormalizeIconLookupName(lookupName);
                string?imageUrl = null;
                if (itemIconUrlCache.TryGetValue(key, out string?cachedUrl)) {
                    imageUrl = cachedUrl;
                } else {
                    imageUrl = await FindFanApiImageUrlAsync(endpoint, lookupName);
                    if (!string.IsNullOrWhiteSpace(imageUrl)) itemIconUrlCache[key] = imageUrl;
                }
                if (string.IsNullOrWhiteSpace(imageUrl)) continue;
                for (int attempt = 0; attempt<2; attempt++) {
                    try {
                        byte[] bytes = await itemIconHttpClient.GetByteArrayAsync(imageUrl);
                        if (bytes.Length<64) break;
                        string tempPath = outputPath + ".tmp";
                        await File.WriteAllBytesAsync(tempPath, bytes);
                        if (File.Exists(outputPath)) File.Delete(outputPath);
                        File.Move(tempPath, outputPath);
                        return true;
                    } catch {
                        if (attempt == 0) await System.Threading.Tasks.Task.Delay(180);
                    }
                }
            }
            return false;
        }
        private async System.Threading.Tasks.Task<string?> FindFanApiImageUrlAsync(string endpoint, string lookupName) {
            string url = "https://eldenring.fanapis.com/api/" + endpoint + "?name=" + Uri.EscapeDataString(lookupName) + "&limit=20";
            for (int attempt = 0; attempt<2; attempt++) {
                try {
                    string json = await itemIconHttpClient.GetStringAsync(url);
                    using JsonDocument document = JsonDocument.Parse(json);
                    if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array) return null;
                    string normalizedLookup = NormalizeIconLookupName(lookupName);
                    string?fallback = null;
                    foreach (JsonElement candidate in data.EnumerateArray()) {
                        string candidateName = candidate.TryGetProperty("name", out JsonElement n)? n.GetString() ?? "" : "";
                        if (!candidate.TryGetProperty("image", out JsonElement image) || image.ValueKind != JsonValueKind.String) continue;
                        string?candidateUrl = image.GetString();
                        if (string.IsNullOrWhiteSpace(candidateUrl)) continue;
                        fallback ??= candidateUrl;
                        if (NormalizeIconLookupName(candidateName) == normalizedLookup) return candidateUrl;
                    }
                    return fallback;
                } catch {
                    if (attempt == 0) await System.Threading.Tasks.Task.Delay(180);
                }
            }
            return null;
        }
        private static string[] GetItemIconLookupNames(string itemName) {
            List<string> names = new List<string>();
            void Add(string value) {
                string v = value.Trim();
                if (v.Length> 0 &&!names.Any(x => x.Equals(v, StringComparison.OrdinalIgnoreCase))) names.Add(v);
            }
            Add(itemName);
            string baseName = Regex.Replace(itemName, @"\s*\(Altered\)\s*$", "", RegexOptions.IgnoreCase);
            Add(baseName);
            Add(itemName.Replace("’", "'"));
            Add(baseName.Replace("’", "'"));
            return names.ToArray();
        }
        private static string NormalizeIconLookupName(string value) {
            string normalized = value.Replace("’", "'").Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\s*\(altered\)\s*$", "");
            normalized = Regex.Replace(normalized, @"\s+", " ");
            return normalized;
        }
        private static string GetFanApiEndpoint(EldenRingItem item) {
            string category = item.Category.Trim();
            string type = item.Type.ToLowerInvariant();
            if (category.Equals("Armor", StringComparison.OrdinalIgnoreCase) || type.Contains("helmet") || type.Contains("chest") || type.Contains("gauntlet") || type.Contains("greave")) return "armors";
            return "weapons";
        }
        private List<string> GetImportedArmorTypes() {
            return detectedModFiles.Select(x => x.Type).Where(IsArmorEquipmentType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetTypeSortOrder).ToList();
        }
        private bool IsMultiArmorTargeting() {
            List<string> sourceTypes = detectedModFiles.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return sourceTypes.Count> 1 && sourceTypes.All(IsArmorEquipmentType);
        }
        private EldenRingItem? FindArmorTargetPiece(string type, string modelId) {
            return itemDatabase.FirstOrDefault(x => x.Category == "Armor" && x.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && x.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        }
        private void ApplyBaseArmorTarget(EldenRingItem baseItem) {
            if (!IsMultiArmorTargeting()) return;
            itemReplaceBaseTargetModelId = baseItem.ModelId;
            itemReplacePieceTargets.Clear();
            foreach (string type in GetImportedArmorTypes()) {
                EldenRingItem? piece = FindArmorTargetPiece(type, baseItem.ModelId);
                if (piece != null) itemReplacePieceTargets[type] = piece;
            }
            RefreshTargetPieceOverridePanel();
        }
        private EldenRingItem? GetEffectiveTargetForSource(DetectedModFile source) {
            if (IsMultiArmorTargeting()) {
                return itemReplacePieceTargets.TryGetValue(source.Type, out EldenRingItem? piece)? piece : null;
            }
            return targetItemComboBox?.SelectedItem as EldenRingItem;
        }
        private List<string> GetMissingTargetTypes() {
            if (!IsMultiArmorTargeting()) return new List<string>();
            return GetImportedArmorTypes().Where(type =>!itemReplacePieceTargets.ContainsKey(type)).ToList();
        }
        private string BuildCurrentTargetMappingSummary() {
            if (!IsMultiArmorTargeting()) {
                if (targetItemComboBox?.SelectedItem is EldenRingItem single) return single.Name + " / model ID " + single.ModelId;
                return "No target selected";
            }
            return string.Join(" | ", GetImportedArmorTypes().Select(type => {
                if (itemReplacePieceTargets.TryGetValue(type, out EldenRingItem? item)) return type + ": " + item.Name + " [" + item.ModelId + "]"; return type + ": choose manually";
            }));
        }
        private void RefreshTargetPieceOverridePanel() {
            if (targetPieceOverridePanel == null) return;
            targetPieceOverridePanel.Children.Clear();
            List<string> sourceTypes = detectedModFiles.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetTypeSortOrder).ToList();
            if (sourceTypes.Count == 0) {
                targetPieceOverridePanel.Visibility = Visibility.Collapsed;
                return;
            }
            bool multiArmor = IsMultiArmorTargeting();
            bool singleType = sourceTypes.Count == 1;
            if (!multiArmor &&!singleType) {
                targetPieceOverridePanel.Visibility = Visibility.Collapsed;
                return;
            }
            targetPieceOverridePanel.Visibility = Visibility.Visible;
            targetPieceOverridePanel.Children.Add(new TextBlock {
                Text = multiArmor? "TARGET PIECES" : "TARGET PIECE", Foreground = GetResourceBrush("TextSoft"), FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 12, 0, 5)
            });
            IEnumerable<string> rows = multiArmor? GetImportedArmorTypes() : sourceTypes;
            foreach (string type in rows) {
                Grid row = new Grid {
                    Margin = new Thickness(0, 3, 0, 3)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(120)
                });
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = new GridLength(1, GridUnitType.Star)
                });
                row.ColumnDefinitions.Add(new ColumnDefinition {
                    Width = GridLength.Auto
                });
                TextBlock typeText = new TextBlock {
                    Text = type, Foreground = GetResourceBrush("TextMuted"), VerticalAlignment = VerticalAlignment.Center
                };
                EldenRingItem? target = null;
                if (multiArmor) {
                    itemReplacePieceTargets.TryGetValue(type, out target);
                } else {
                    target = targetItemComboBox?.SelectedItem as EldenRingItem;
                }
                bool hasTarget = target != null;
                TextBlock valueText = new TextBlock {
                    Text = hasTarget? target!.Name + " · ID " + target.ModelId : "No target selected", Foreground = hasTarget? GetResourceBrush("TextMain") : warningBrush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0, 8, 0)
                };
                Button chooseButton = new Button {
                    Content = hasTarget? "Change..." : "Choose...", Style = (Style) FindResource("ActionButton"), MinWidth = 86, Padding = new Thickness(10, 4, 10, 4)
                };
                string capturedType = type;
                chooseButton.Click += (_, _) => {
                    OpenVisualTargetPicker(capturedType, selected => {
                        if (multiArmor) {
                            itemReplacePieceTargets[capturedType] = selected;
                        } else if (targetItemComboBox != null) {
                            isRestoringItemReplaceUi = true; targetItemComboBox.SelectedItem = selected; targetItemComboBox.Text = selected.Name; itemReplaceSelectedTargetFileName = selected.FileName; isRestoringItemReplaceUi = false;
                        }
                        RefreshTargetPieceOverridePanel(); UpdateSelectedTargetInformation(); ResetConflictState(); UpdateItemReplaceStepStates();
                    });
                };
                Grid.SetColumn(typeText, 0);
                Grid.SetColumn(valueText, 1);
                Grid.SetColumn(chooseButton, 2);
                row.Children.Add(typeText);
                row.Children.Add(valueText);
                row.Children.Add(chooseButton);
                targetPieceOverridePanel.Children.Add(row);
            }
            if (multiArmor) {
                List<string> missing = GetMissingTargetTypes();
                if (missing.Count> 0) {
                    targetPieceOverridePanel.Children.Add(new TextBlock {
                        Text = "Selected set is missing: " + string.Join(", ", missing) + ". Choose those pieces manually before compatibility check.", Foreground = warningBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
                    });
                } else if (itemReplacePieceTargets.Values.Select(x => x.ModelId).Distinct(StringComparer.OrdinalIgnoreCase).Count()> 1) {
                    targetPieceOverridePanel.Children.Add(new TextBlock {
                        Text = "Custom target mapping active · one or more pieces come from another set.", Foreground = GetResourceBrush("TextMuted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
                    });
                }
            }
        }
        private void OpenVisualTargetPicker(string?forcedType = null, Action<EldenRingItem>? selectionCallback = null) {
            List<EldenRingItem> visualCandidates = forcedType == null? itemReplaceTargetCandidates.ToList() : itemDatabase.Where(x => x.Type.Equals(forcedType, StringComparison.OrdinalIgnoreCase) &&!IsTechnicalBlankItem(x)).OrderBy(x => int.TryParse(x.ModelId, out int id)? id : int.MaxValue).ThenBy(x => x.Name).ToList();
            if (targetItemComboBox == null || visualCandidates.Count == 0) {
                ShowToast(forcedType == null? "Import a mod source first" : "No compatible " + forcedType + " targets found", ToastKind.Info);
                return;
            }
            Window window = new Window {
                Title = "Visual Replacement Target", Width = 920, Height = 680, MinWidth = 760, MinHeight = 540, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = GetResourceBrush("BgMain"), Foreground = GetResourceBrush("TextMain")
            };
            foreach (System.Collections.DictionaryEntry resource in Resources) {
                window.Resources[resource.Key] = resource.Value;
            }
            Grid root = new Grid {
                Margin = new Thickness(18)
            };
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition {
                Height = GridLength.Auto
            });
            StackPanel heading = new StackPanel();
            heading.Children.Add(new TextBlock {
                Text = forcedType == null? "Choose Replacement Target" : "Choose " + forcedType + " Target", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = GetResourceBrush("TextMain")
            });
            heading.Children.Add(new TextBlock {
                Text = forcedType == null? "Browse compatible items visually. Double-click an item or select it below." : "Choose a manual " + forcedType + " override. This changes only that imported piece.", Foreground = GetResourceBrush("TextMuted"), Margin = new Thickness(0, 4, 0, 0)
            });
            root.Children.Add(heading);
            Grid filterRow = new Grid {
                Margin = new Thickness(0, 14, 0, 12)
            };
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(500)
            });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(14)
            });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(10)
            });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(230)
            });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            Grid searchHost = new Grid {
                Height = 32
            };
            TextBox searchBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Height = 32, VerticalContentAlignment = VerticalAlignment.Center
            };
            TextBlock searchPlaceholder = new TextBlock {
                Text = "Search item name or ID...", Foreground = GetResourceBrush("TextMuted"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            };
            searchHost.Children.Add(searchBox);
            searchHost.Children.Add(searchPlaceholder);
            TextBlock filterLabel = new TextBlock {
                Text = "Filter:", Foreground = GetResourceBrush("TextSoft"), VerticalAlignment = VerticalAlignment.Center
            };
            string selectedVisualType = "All Types";
            List<string> typeOptions = new List<string> {
                "All Types"
            };
            typeOptions.AddRange(visualCandidates.Select(x => x.Type).Where(x =>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => GetTypeSortOrder(x)).ThenBy(x => x));
            ComboBox typeFilterComboBox = new ComboBox {
                Style = (Style) window.FindResource("DarkComboBox"), Height = 32, MinWidth = 230, ItemsSource = typeOptions, SelectedItem = "All Types", IsEditable = false, HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(searchHost, 0);
            Grid.SetColumn(filterLabel, 2);
            Grid.SetColumn(typeFilterComboBox, 4);
            filterRow.Children.Add(searchHost);
            filterRow.Children.Add(filterLabel);
            filterRow.Children.Add(typeFilterComboBox);
            Grid.SetRow(filterRow, 1);
            root.Children.Add(filterRow);
            Border listBorder = new Border {
                BorderBrush = GetResourceBrush("BorderMain"), BorderThickness = new Thickness(1), Background = GetResourceBrush("BgPanel"), Padding = new Thickness(10)
            };
            ScrollViewer scroller = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            WrapPanel cards = new WrapPanel {
                Orientation = Orientation.Horizontal
            };
            scroller.Content = cards;
            listBorder.Child = scroller;
            Grid.SetRow(listBorder, 2);
            root.Children.Add(listBorder);
            EldenRingItem? selectedItem = null;
            Grid footer = new Grid {
                Margin = new Thickness(0, 12, 0, 0)
            };
            footer.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            footer.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            TextBlock selectedText = new TextBlock {
                Text = "No item selected", Foreground = GetResourceBrush("TextMuted"), VerticalAlignment = VerticalAlignment.Center
            };
            Button selectButton = new Button {
                Content = "Select", IsEnabled = false, MinWidth = 112, Height = 36, HorizontalAlignment = HorizontalAlignment.Right, Foreground = GetResourceBrush("TextSoft"), Background = GetResourceBrush("BgPanel2"), BorderBrush = GetResourceBrush("BorderMain"), BorderThickness = new Thickness(1), Cursor = Cursors.Hand
            };
            Grid.SetColumn(selectedText, 0);
            Grid.SetColumn(selectButton, 1);
            footer.Children.Add(selectedText);
            footer.Children.Add(selectButton);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            int renderVersion = 0;
            async System.Threading.Tasks.Task LoadCardIconAsync(EldenRingItem item, Image image, TextBlock status, int version) {
                string highResPath = GetDatabaseGameItemIconCachePath(item);
                string?iconPath = File.Exists(highResPath)? highResPath : await EnsureDatabaseItemIconCachedAsync(item);
                if (version != renderVersion) return;
                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath)) {
                    try {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        bool isGameHighRes = iconPath.IndexOf(Path.DirectorySeparatorChar + "ItemIconsGameV7" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
                        RenderOptions.SetBitmapScalingMode(image, isGameHighRes? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
                        image.Source = bitmap;
                        status.Visibility = Visibility.Collapsed;
                        if (!isGameHighRes && HasValidGameDataDirectory() &&!fullIconPrecacheRunning) {
                            _ = UpgradeVisualCardIconAsync(item, image, status, version);
                        }
                        return;
                    } catch {
                    }
                }
                status.Text = "No icon";
            }
            async System.Threading.Tasks.Task UpgradeVisualCardIconAsync(EldenRingItem item, Image image, TextBlock status, int version) {
                string highResPath = GetDatabaseGameItemIconCachePath(item);
                if (File.Exists(highResPath)) return;
                await VisualIconUpgradeGate.WaitAsync();
                try {
                    if (File.Exists(highResPath)) return;
                    bool upgraded = await EldenRingGameIconService.TryCreateHighResolutionIconAsync(gameDataFolder, item, highResPath);
                    if (!upgraded || version != renderVersion ||!window.IsVisible ||!File.Exists(highResPath)) return;
                    await window.Dispatcher.InvokeAsync(() => {
                        if (version != renderVersion ||!window.IsVisible) return; try {
                            BitmapImage bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(highResPath, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze(); RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); RenderOptions.SetEdgeMode(image, EdgeMode.Unspecified); image.Source = bitmap; status.Visibility = Visibility.Collapsed;
                        } catch {
                        }
                    });
                } finally {
                    VisualIconUpgradeGate.Release();
                }
            }
            void UpdateSelectButton() {
                bool enabled = selectedItem != null;
                selectButton.IsEnabled = enabled;
                selectButton.Foreground = enabled? GetResourceBrush("TextMain") : GetResourceBrush("TextMuted");
                selectButton.Background = enabled? GetResourceBrush("Accent") : GetResourceBrush("BgPanel2");
                selectButton.BorderBrush = enabled? GetResourceBrush("AccentBright") : GetResourceBrush("BorderMain");
                selectedText.Text = selectedItem == null? "No item selected" : (forcedType == null && IsMultiArmorTargeting()? "Target set · ID " + selectedItem.ModelId + " · click Select to auto-map available pieces" : selectedItem.Name + " · ID " + selectedItem.ModelId);
            }
            void RenderCards() {
                renderVersion++;
                int version = renderVersion;
                cards.Children.Clear();
                string search = searchBox.Text.Trim();
                string selectedType = selectedVisualType;
                List<EldenRingItem> filtered = visualCandidates.Where(item => (string.IsNullOrWhiteSpace(search) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || item.ModelId.Contains(search, StringComparison.OrdinalIgnoreCase)) && (selectedType == "All Types" || item.Type.Equals(selectedType, StringComparison.OrdinalIgnoreCase))).Take(48).ToList();
                foreach (EldenRingItem item in filtered) {
                    bool isSelected = selectedItem != null && (ReferenceEquals(selectedItem, item) || (forcedType == null && IsMultiArmorTargeting() && selectedItem.ModelId == item.ModelId));
                    Border card = new Border {
                        Width = 164, Height = 204, Background = GetResourceBrush("BgMain"), BorderBrush = isSelected? GetResourceBrush("AccentBright") : GetResourceBrush("BorderMain"), BorderThickness = new Thickness(isSelected? 2 : 1), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 10, 10), Cursor = Cursors.Hand
                    };
                    StackPanel stack = new StackPanel {
                        Margin = new Thickness(9)
                    };
                    Grid imageFrame = new Grid {
                        Width = 144, Height = 136, Background = GetResourceBrush("BgPanel2")
                    };
                    Image image = new Image {
                        Width = 128, Height = 128, Stretch = Stretch.Uniform, SnapsToDevicePixels = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                    RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
                    TextBlock status = new TextBlock {
                        Text = "Loading...", Foreground = GetResourceBrush("TextMuted"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    imageFrame.Children.Add(image);
                    imageFrame.Children.Add(status);
                    stack.Children.Add(imageFrame);
                    stack.Children.Add(new TextBlock {
                        Text = item.Name, Foreground = GetResourceBrush("TextMain"), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 36, Margin = new Thickness(0, 7, 0, 2)
                    });
                    stack.Children.Add(new TextBlock {
                        Text = item.Type + " · ID " + item.ModelId, Foreground = GetResourceBrush("TextMuted"), FontSize = 11
                    });
                    card.Child = stack;
                    card.MouseEnter += (_, _) => {
                        bool partOfSelectedSet = selectedItem != null && forcedType == null && IsMultiArmorTargeting() && selectedItem.ModelId == item.ModelId;
                        if (!ReferenceEquals(selectedItem, item) &&!partOfSelectedSet) {
                            card.BorderBrush = GetResourceBrush("Accent");
                            card.BorderThickness = new Thickness(1.25);
                        }
                    };
                    card.MouseLeave += (_, _) => {
                        bool partOfSelectedSet = selectedItem != null && forcedType == null && IsMultiArmorTargeting() && selectedItem.ModelId == item.ModelId;
                        if (!ReferenceEquals(selectedItem, item) &&!partOfSelectedSet) {
                            card.BorderBrush = GetResourceBrush("BorderMain");
                            card.BorderThickness = new Thickness(1);
                        }
                    };
                    card.MouseLeftButtonUp += async(_, e) => {
                        selectedItem = item;
                        UpdateSelectButton();
                        if (e.ClickCount >= 2) {
                            if (selectionCallback != null) {
                                selectionCallback(item);
                            } else {
                                targetItemComboBox.SelectedItem = item;
                                targetItemComboBox.Text = item.Name;
                                itemReplaceSelectedTargetFileName = item.FileName;
                                if (IsMultiArmorTargeting()) ApplyBaseArmorTarget(item);
                            }
                            window.DialogResult = true;
                            window.Close();
                            return;
                        }
                        RenderCards();
                    };
                    cards.Children.Add(card);
                    _ = LoadCardIconAsync(item, image, status, version);
                }
            }
            typeFilterComboBox.SelectionChanged += (_, _) => {
                selectedVisualType = typeFilterComboBox.SelectedItem?.ToString() ?? "All Types";
                RenderCards();
            };
            searchBox.TextChanged += (_, _) => {
                searchPlaceholder.Visibility = string.IsNullOrWhiteSpace(searchBox.Text)? Visibility.Visible : Visibility.Collapsed;
                RenderCards();
            };
            selectButton.Click += (_, _) => {
                if (selectedItem == null) return;
                if (selectionCallback != null) {
                    selectionCallback(selectedItem);
                } else {
                    targetItemComboBox.SelectedItem = selectedItem;
                    targetItemComboBox.Text = selectedItem.Name;
                    itemReplaceSelectedTargetFileName = selectedItem.FileName;
                    if (IsMultiArmorTargeting()) ApplyBaseArmorTarget(selectedItem);
                }
                window.DialogResult = true;
                window.Close();
            };
            window.Content = root;
            window.Loaded += (_, _) => {
                RenderCards();
                searchBox.Focus();
            };
            window.ShowDialog();
        }
        private void TargetItemComboBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (isRestoringItemReplaceUi || targetItemComboBox == null ||!targetItemComboBox.IsKeyboardFocusWithin) {
                return;
            }
            string query = targetItemComboBox.Text?.Trim() ?? "";
            List<EldenRingItem> suggestions = itemReplaceTargetCandidates.Where(item => string.IsNullOrWhiteSpace(query) || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || item.ModelId.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(item => string.IsNullOrWhiteSpace(query)? 0 : item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)? 0 : item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)? 1 : 2).ThenBy(item => GetTypeSortOrder(item.Type)).ThenBy(item => item.Name).Take(80).ToList();
            isRestoringItemReplaceUi = true;
            targetItemComboBox.ItemsSource = null;
            targetItemComboBox.ItemsSource = suggestions;
            targetItemComboBox.SelectedItem = null;
            targetItemComboBox.Text = query;
            isRestoringItemReplaceUi = false;
            itemReplaceSelectedTargetFileName = "";
            UpdateSelectedTargetInformation();
            UpdateItemReplaceStepStates();
            targetItemComboBox.IsDropDownOpen = suggestions.Count> 0;
        }
        private void TargetItemComboBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (targetItemComboBox == null) {
                return;
            }
            if (e.Key == Key.Down) {
                targetItemComboBox.IsDropDownOpen = true;
                return;
            }
            if (e.Key != Key.Enter) {
                return;
            }
            string query = targetItemComboBox.Text?.Trim() ?? "";
            EldenRingItem? match = itemReplaceTargetCandidates.Where(item => string.IsNullOrWhiteSpace(query) || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || item.ModelId.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Name.Equals(query, StringComparison.OrdinalIgnoreCase)? 0 : item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)? 1 : 2).ThenBy(item => GetTypeSortOrder(item.Type)).ThenBy(item => item.Name).FirstOrDefault();
            if (match == null) {
                return;
            }
            isRestoringItemReplaceUi = true;
            targetItemComboBox.ItemsSource = null;
            targetItemComboBox.ItemsSource = itemReplaceTargetCandidates;
            targetItemComboBox.SelectedItem = match;
            targetItemComboBox.Text = match.Name;
            isRestoringItemReplaceUi = false;
            itemReplaceSelectedTargetFileName = match.FileName;
            UpdateSelectedTargetInformation();
            ResetConflictState();
            UpdateItemReplaceStepStates();
            targetItemComboBox.IsDropDownOpen = false;
            e.Handled = true;
        }
        private void TargetItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (targetItemComboBox?.SelectedItem is EldenRingItem selected) {
                targetItemComboBox.Text = selected.Name;
                itemReplaceSelectedTargetFileName = selected.FileName;
                if (!isRestoringItemReplaceUi && IsMultiArmorTargeting()) ApplyBaseArmorTarget(selected);
            }
            UpdateSelectedTargetInformation();
            if (!isRestoringItemReplaceUi) ResetConflictState();
            UpdateItemReplaceStepStates();
        }
        private void UpdateSelectedTargetInformation() {
            if (selectedItemNameText == null) return;
            if (targetItemComboBox?.SelectedItem is not EldenRingItem item) {
                selectedItemNameText.Text = "Name: -";
                if (selectedItemTypeText != null) selectedItemTypeText.Text = "Type: -";
                if (selectedItemIdText != null) selectedItemIdText.Text = "Model ID: -";
                if (selectedItemFileText != null) selectedItemFileText.Text = "Target File: -";
                RefreshTargetPieceOverridePanel();
                return;
            }
            if (IsMultiArmorTargeting()) {
                int mapped = itemReplacePieceTargets.Count;
                int required = GetImportedArmorTypes().Count;
                selectedItemNameText.Text = "Base Set: " + item.Name;
                selectedItemTypeText!.Text = "Mapped Pieces: " + mapped + " / " + required;
                selectedItemIdText!.Text = "Base Model ID: " + item.ModelId;
                selectedItemFileText!.Text = "Mapping: " + BuildCurrentTargetMappingSummary();
                selectedItemFileText.TextWrapping = TextWrapping.Wrap;
            } else {
                selectedItemNameText.Text = "Name: " + item.Name;
                selectedItemTypeText!.Text = "Type: " + item.Type;
                selectedItemIdText!.Text = "Model ID: " + item.ModelId;
                selectedItemFileText!.Text = "Target File: " + item.FileName;
            }
            RefreshTargetPieceOverridePanel();
        }
        private void BrowseModFolder_Click(object sender, RoutedEventArgs e) {
            OpenFolderDialog dialog = new OpenFolderDialog {
                Title = "Select Elden Ring Skin Mod Folder"
            };
            bool?result = dialog.ShowDialog();
            if (result != true) {
                return;
            }
            string folder = dialog.FolderName;
            try {
                itemReplaceSourceDisplay = folder;
                itemReplaceSourceInfo = "Source: Folder - " + Path.GetFileName(folder);
                if (modFolderTextBox != null) {
                    modFolderTextBox.Text = itemReplaceSourceDisplay;
                }
                if (modFolderInfoText != null) {
                    modFolderInfoText.Text = itemReplaceSourceInfo;
                }
                string[] sourceFiles;
                try {
                    sourceFiles = Directory.GetFiles(folder, "*.partsbnd.dcx", SearchOption.AllDirectories);
                } catch {
                    sourceFiles = Array.Empty<string>();
                }
                itemReplaceSourcePaths.Clear();
                itemReplaceSourcePaths.AddRange(sourceFiles);
                ScanSelectedFiles(itemReplaceSourcePaths);
                ResetConflictState();
                RestoreCompatibilityState();
                UpdateItemReplaceStepStates();
                StatusText.Text = "Skin > Item Replace > Folder imported";
            } catch (Exception ex) {
                MessageBox.Show("The selected mod folder could not be loaded.\n\n" + ex.Message, "Elden Ring Toolkit", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Skin > Item Replace > Import failed";
            }
        }
        private void BrowseModFile_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog {
                Title = "Select Elden Ring part file", Filter = "Elden Ring Part Files (*.partsbnd.dcx)|*.partsbnd.dcx|All Files (*.*)|*.*", Multiselect = true
            };
            bool?result = dialog.ShowDialog();
            if (result != true) {
                return;
            }
            string[] files = dialog.FileNames;
            if (files.Length == 0) {
                return;
            }
            try {
                itemReplaceSourcePaths.Clear();
                itemReplaceSourcePaths.AddRange(files);
                if (files.Length == 1) {
                    itemReplaceSourceDisplay = files[0];
                    itemReplaceSourceInfo = "Source: File - " + Path.GetFileName(files[0]);
                } else {
                    itemReplaceSourceDisplay = files.Length + " files selected";
                    itemReplaceSourceInfo = "Source: " + files.Length + " individual files";
                }
                if (modFolderTextBox != null) {
                    modFolderTextBox.Text = itemReplaceSourceDisplay;
                }
                if (modFolderInfoText != null) {
                    modFolderInfoText.Text = itemReplaceSourceInfo;
                }
                ScanSelectedFiles(itemReplaceSourcePaths);
                ResetConflictState();
                RestoreCompatibilityState();
                UpdateItemReplaceStepStates();
                StatusText.Text = "Skin > Item Replace > File import";
            } catch (Exception ex) {
                MessageBox.Show("The selected mod file could not be loaded.\n\n" + ex.Message, "Elden Ring Toolkit", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Skin > Item Replace > Import failed";
            }
        }
        private void ScanModFolder(string folder) {
            string[] files;
            try {
                files = Directory.GetFiles(folder, "*.partsbnd.dcx", SearchOption.AllDirectories);
            } catch {
                files = Array.Empty<string>();
            }
            ScanSelectedFiles(files);
        }
        private void ScanSelectedFiles(IEnumerable<string> files) {
            detectedModFiles.Clear();
            foreach (string fullPath in files) {
                string fileName = Path.GetFileName(fullPath);
                ParsedPartFile parsed = ParsePartFileName(fileName);
                if (parsed.Type == "Unknown") continue;
                detectedModFiles.Add(new DetectedModFile {
                    FullPath = fullPath, FileName = fileName, Type = parsed.Type, ModelId = parsed.ModelId
                });
            }
            UpdateDetectedFileSummary();
            ApplyTargetFilterFromDetectedSource();
        }
        private void ApplyTargetFilterFromDetectedSource() {
            if (targetItemComboBox == null) return;
            EldenRingItem? previousTarget = targetItemComboBox.SelectedItem as EldenRingItem;
            List<string> sourceTypes = detectedModFiles.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetTypeSortOrder).ToList();
            bool hasDuplicateSourceTypes = HasDuplicateLogicalSourceTypes();
            List<EldenRingItem> candidates = new List<EldenRingItem>();
            if (hasDuplicateSourceTypes) {
                itemReplacePieceTargets.Clear();
                itemReplaceBaseTargetModelId = "";
                if (targetFilterHintText != null) {
                    targetFilterHintText.Text = "Multiple imported files use the same equipment type. " + "Item Replace can map only one file per type in a single operation.";
                }
            } else if (sourceTypes.Count == 0) {
                itemReplacePieceTargets.Clear();
                itemReplaceBaseTargetModelId = "";
                if (targetFilterHintText != null) targetFilterHintText.Text = "Import a mod source first.";
            } else if (sourceTypes.Count == 1) {
                itemReplacePieceTargets.Clear();
                itemReplaceBaseTargetModelId = "";
                string sourceType = sourceTypes[0];
                candidates = itemDatabase.Where(x => x.Type == sourceType).OrderBy(x => int.TryParse(x.ModelId, out int id)? id : int.MaxValue).ThenBy(x => x.Name).ToList();
                if (targetFilterHintText != null) {
                    targetFilterHintText.Text = "Detected weapon/item type: " + sourceType + ". Use Change below to choose a matching target.";
                }
            } else if (sourceTypes.All(IsArmorEquipmentType)) {
                HashSet<string> sourceTypeSet = sourceTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> coverageByModel = itemDatabase.Where(x => x.Category == "Armor" && sourceTypeSet.Contains(x.Type) &&!IsTechnicalBlankItem(x)).GroupBy(x => x.ModelId).ToDictionary(g => g.Key, g => g.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).Count(), StringComparer.OrdinalIgnoreCase);
                candidates = itemDatabase.Where(x => x.Category == "Armor" && sourceTypeSet.Contains(x.Type) &&!IsTechnicalBlankItem(x)).OrderByDescending(x => coverageByModel.TryGetValue(x.ModelId, out int count)? count : 0).ThenBy(x => int.TryParse(x.ModelId, out int id)? id : int.MaxValue).ThenBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => x.Name).ToList();
                if (targetFilterHintText != null) {
                    targetFilterHintText.Text = "Detected armor parts: " + string.Join(", ", sourceTypes) + ". Complete sets are recommended first. Partial sets are also allowed; " + "missing pieces can be chosen manually below.";
                }
            } else {
                itemReplacePieceTargets.Clear();
                itemReplaceBaseTargetModelId = "";
                if (targetFilterHintText != null) {
                    targetFilterHintText.Text = "Mixed armor + weapon/customization types cannot share one replacement operation. " + "Use one operation per non-armor file.";
                }
            }
            isRestoringItemReplaceUi = true;
            itemReplaceTargetCandidates.Clear();
            itemReplaceTargetCandidates.AddRange(candidates);
            targetItemComboBox.ItemsSource = null;
            targetItemComboBox.ItemsSource = itemReplaceTargetCandidates;
            EldenRingItem? selectedTarget = null;
            if (previousTarget != null) {
                selectedTarget = candidates.FirstOrDefault(x => x.FileName.Equals(previousTarget.FileName, StringComparison.OrdinalIgnoreCase));
            }
            if (selectedTarget == null &&!string.IsNullOrWhiteSpace(itemReplaceSelectedTargetFileName)) {
                selectedTarget = candidates.FirstOrDefault(x => x.FileName.Equals(itemReplaceSelectedTargetFileName, StringComparison.OrdinalIgnoreCase));
            }
            if (selectedTarget == null && detectedModFiles.Count> 0) {
                DetectedModFile firstSource = detectedModFiles.OrderBy(x => GetTypeSortOrder(x.Type)).First();
                selectedTarget = candidates.FirstOrDefault(x => x.Type == firstSource.Type && x.ModelId == firstSource.ModelId);
            }
            targetItemComboBox.SelectedItem = selectedTarget;
            if (selectedTarget != null) {
                targetItemComboBox.Text = selectedTarget.Name;
                itemReplaceSelectedTargetFileName = selectedTarget.FileName;
                if (sourceTypes.Count> 1 && sourceTypes.All(IsArmorEquipmentType)) {
                    bool shouldRebuildMapping = itemReplacePieceTargets.Count == 0 ||!itemReplaceBaseTargetModelId.Equals(selectedTarget.ModelId, StringComparison.OrdinalIgnoreCase);
                    if (shouldRebuildMapping) ApplyBaseArmorTarget(selectedTarget);
                }
            } else {
                targetItemComboBox.Text = "";
            }
            isRestoringItemReplaceUi = false;
            RefreshTargetPieceOverridePanel();
            UpdateSelectedTargetInformation();
            UpdateItemReplaceStepStates();
        }
        private void UpdateDetectedFileSummary() {
            if (detectedModTypeText == null || detectedModItemText == null || detectedModFileText == null) {
                return;
            }
            if (detectedModFiles.Count == 0) {
                detectedModTypeText.Text = "Detected Types: None";
                detectedModItemText.Text = "Detected Items: None";
                detectedModFileText.Text = "Detected Files: None";
                return;
            }
            List<string> types = detectedModFiles.Select(x => x.Type).Distinct().OrderBy(x => GetTypeSortOrder(x)).ToList();
            detectedModTypeText.Text = "Detected Types: " + string.Join(", ", types);
            IEnumerable<string> itemLines = detectedModFiles.GroupBy(x => new {
                x.Type, x.ModelId
            }).OrderBy(x => GetTypeSortOrder(x.Key.Type)).ThenBy(x => int.TryParse(x.Key.ModelId, out int number)? number : int.MaxValue).Select(x => "• " + x.Key.Type + " / " + x.Key.ModelId + ": " + FindItemDisplayName(x.Key.Type, x.Key.ModelId));
            detectedModItemText.Text = "Detected Items:\n" + string.Join("\n", itemLines);
            IEnumerable<string> fileLines = detectedModFiles.OrderBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => x.FileName).Select(x => "• " + x.FileName + "  [" + x.Type + "]");
            detectedModFileText.Text = "Detected Files (" + detectedModFiles.Count + "):\n" + string.Join("\n", fileLines);
        }
        private int GetTypeSortOrder(string type) {
            return type switch {
                "Helmet" => 1, "Chest Armor" => 2, "Gauntlet" => 3, "Greaves" => 4, "Dagger" => 10, "Straight Sword" => 11, "Greatsword" => 12, "Colossal Sword" => 13, "Thrusting Sword" => 14, "Heavy Thrusting Sword" => 15, "Curved Sword" => 16, "Curved Greatsword" => 17, "Katana" => 18, "Great Katana" => 19, "Light Greatsword" => 20, "Twinblade" => 21, "Backhand Blade" => 22, "Axe" => 23, "Greataxe" => 24, "Hammer" => 25, "Great Hammer" => 26, "Flail" => 27, "Spear" => 28, "Great Spear" => 29, "Halberd" => 30, "Reaper" => 31, "Whip" => 32, "Fist" => 33, "Claw" => 34, "Beast Claw" => 35, "Colossal Weapon" => 36, "Torch" => 37, "Thrusting Shield" => 38, "Shield" => 39, "Greatshield" => 40, "Glintstone Staff" => 41, "Sacred Seal" => 42, "Shortbow" => 43, "Bow" => 44, "Greatbow" => 45, "Crossbow" => 46, "Ballista" => 47, "Perfume Bottle" => 48, "Arrow" => 49, "Great Arrow" => 50, "Bolt" => 51, "Greatbolt" => 52, "Weapon" => 59, "Hairstyle" => 70, "Face" => 71, "Eyes" => 72, "Eyebrows" => 73, "Facial Hair" => 74, "Accessory" => 75, "Tattoo / Mark" => 76, "Eyelashes" => 77, "Body" => 78, "Face Part" => 79, _ => 99
            };
        }
        private void CheckCompatibility_Click(object sender, RoutedEventArgs e) {
            currentRecommendation = null;
            if (targetItemComboBox?.SelectedItem is not EldenRingItem target) {
                SetCompatibilityWarning("Choose a replacement target first.");
                UpdateItemReplaceStepStates();
                return;
            }
            bool hasDuplicateSourceTypes = HasDuplicateLogicalSourceTypes();
            if (hasDuplicateSourceTypes) {
                SetCompatibilityWarning("Multiple imported files use the same equipment type. " + "One Item Replace operation can map only one file per type.");
                UpdateItemReplaceStepStates();
                return;
            }
            if (detectedModFiles.Count == 0) {
                SetCompatibilityWarning("Import a supported .partsbnd.dcx mod source first.");
                UpdateItemReplaceStepStates();
                return;
            }
            List<string> sourceTypes = detectedModFiles.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetTypeSortOrder).ToList();
            if (sourceTypes.Count == 1) {
                string sourceType = sourceTypes[0];
                if (!string.Equals(target.Type, sourceType, StringComparison.OrdinalIgnoreCase)) {
                    SetCompatibilityWarning("Incompatible target. Imported mod type: " + sourceType + ". Choose a " + sourceType + " target item.");
                    UpdateItemReplaceStepStates();
                    return;
                }
            } else if (sourceTypes.All(IsArmorEquipmentType)) {
                List<string> missing = GetMissingTargetTypes();
                if (missing.Count> 0) {
                    SetCompatibilityWarning("Target mapping is incomplete. Choose a replacement for: " + string.Join(", ", missing) + ".");
                    UpdateItemReplaceStepStates();
                    return;
                }
            } else {
                SetCompatibilityWarning("Mixed armor + weapon/customization files cannot be installed in one Item Replace operation.");
                UpdateItemReplaceStepStates();
                return;
            }
            itemReplaceUiState = ItemReplaceUiState.Compatible;
            itemReplaceUiMessage = "Compatibility passed.\nSource type(s): " + string.Join(", ", sourceTypes) + "\nTarget mapping: " + BuildCurrentTargetMappingSummary();
            compatibilityText!.Text = itemReplaceUiMessage;
            compatibilityText.Foreground = successBrush;
            if (conflictStatusText != null) {
                conflictStatusText.Text = "Compatibility passed. Ready to check the active Mod Engine 2 folder.";
                conflictStatusText.Foreground = warningBrush;
            }
            StatusText.Text = "Skin > Item Replace > Compatibility passed";
            UpdateItemReplaceStepStates();
        }
        private void CheckModFolderOnly_Click(object sender, RoutedEventArgs e) {
            if (itemReplaceUiState != ItemReplaceUiState.Compatible && itemReplaceUiState != ItemReplaceUiState.NoConflict && itemReplaceUiState != ItemReplaceUiState.Conflict) {
                return;
            }
            if (targetItemComboBox?.SelectedItem is not EldenRingItem target) {
                return;
            }
            string activeModRoot = GetModEngineRootFolder();
            if (string.IsNullOrWhiteSpace(activeModRoot) ||!Directory.Exists(activeModRoot)) {
                if (conflictStatusText != null) {
                    conflictStatusText.Text = "No active Mod Engine 2 folder is configured. Open Skin > Mod Engine 2 Folder first.";
                    conflictStatusText.Foreground = warningBrush;
                }
                return;
            }
            ScanInstalledModFolder();
            List<FileConflict> conflicts = FindTargetConflictsForCurrentSelection();
            if (conflicts.Count == 0) {
                itemReplaceUiState = ItemReplaceUiState.NoConflict;
                itemReplaceUiMessage = "No conflict found. All selected target piece slots are free.";
                if (conflictStatusText != null) {
                    conflictStatusText.Text = itemReplaceUiMessage;
                    conflictStatusText.Foreground = successBrush;
                }
                if (conflictActionPanel != null) {
                    conflictActionPanel.Visibility = Visibility.Collapsed;
                }
                StatusText.Text = "Skin > Item Replace > Target slot free";
                UpdateItemReplaceStepStates();
                return;
            }
            currentRecommendation = BuildRecommendation();
            string conflictLines = string.Join("\n", conflicts.Select(c => "• " + c.Existing.FileName + " already occupies " + c.Source.Type + " / " + (GetEffectiveTargetForSource(c.Source)?.ModelId ?? "?")));
            string recommendation = currentRecommendation == null? "\n\nNo free recommendation is available in the current database." : "\n\nRecommended free model ID: " + currentRecommendation.ModelId + "\n" + currentRecommendation.Description;
            itemReplaceUiState = ItemReplaceUiState.Conflict;
            itemReplaceUiMessage = "Conflict detected:\n" + conflictLines + recommendation;
            if (conflictStatusText != null) {
                conflictStatusText.Text = itemReplaceUiMessage;
                conflictStatusText.Foreground = warningBrush;
            }
            if (conflictActionPanel != null) {
                conflictActionPanel.Visibility = Visibility.Visible;
            }
            StatusText.Text = "Skin > Item Replace > Conflict detected";
            UpdateItemReplaceStepStates();
        }
        private void SetCompatibilityWarning(string message) {
            itemReplaceUiState = ItemReplaceUiState.Warning;
            itemReplaceUiMessage = message;
            if (compatibilityText != null) {
                compatibilityText.Text = message;
                compatibilityText.Foreground = warningBrush;
            }
            if (installButton != null) {
                installButton.IsEnabled = false;
            }
            if (conflictActionPanel != null) {
                conflictActionPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void ResetCompatibility() {
            ResetConflictState();
        }
        private void ResetConflictState() {
            currentRecommendation = null;
            itemReplaceUiState = ItemReplaceUiState.NotChecked;
            itemReplaceUiMessage = "Not checked";
            if (compatibilityText != null) {
                compatibilityText.Text = detectedModFiles.Count == 0? "Waiting for a mod source and target." : "Ready to check source and target compatibility.";
                compatibilityText.Foreground = detectedModFiles.Count == 0? mutedTextBrush : warningBrush;
            }
            if (conflictStatusText != null) {
                conflictStatusText.Text = "Compatibility must pass first.";
                conflictStatusText.Foreground = mutedTextBrush;
            }
            if (conflictActionPanel != null) {
                conflictActionPanel.Visibility = Visibility.Collapsed;
            }
            if (installButton != null) {
                installButton.IsEnabled = false;
            }
            UpdateItemReplaceStepStates();
        }
        private void RestoreItemReplaceUi() {
            isRestoringItemReplaceUi = true;
            if (modFolderTextBox != null) modFolderTextBox.Text = itemReplaceSourceDisplay;
            if (modFolderInfoText != null) modFolderInfoText.Text = itemReplaceSourceInfo;
            if (itemReplaceSourcePaths.Count> 0) {
                List<string> existingPaths = itemReplaceSourcePaths.Where(File.Exists).ToList();
                if (existingPaths.Count != itemReplaceSourcePaths.Count) {
                    itemReplaceSourcePaths.Clear();
                    itemReplaceSourcePaths.AddRange(existingPaths);
                }
                ScanSelectedFiles(itemReplaceSourcePaths);
            } else {
                UpdateDetectedFileSummary();
                ApplyTargetFilterFromDetectedSource();
            }
            RestoreCompatibilityState();
            UpdateSelectedTargetInformation();
            isRestoringItemReplaceUi = false;
            UpdateItemReplaceStepStates();
        }
        private void RestoreCompatibilityState() {
            if (compatibilityText == null) {
                return;
            }
            switch (itemReplaceUiState) {
                case ItemReplaceUiState.NotChecked : compatibilityText.Text = detectedModFiles.Count == 0? "Waiting for a mod source and target." : "Ready to check source and target compatibility.";
                compatibilityText.Foreground = detectedModFiles.Count == 0? mutedTextBrush : warningBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = "Compatibility must pass first.";
                    conflictStatusText.Foreground = mutedTextBrush;
                }
                break;
                case ItemReplaceUiState.Warning : compatibilityText.Text = itemReplaceUiMessage;
                compatibilityText.Foreground = errorBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = "Compatibility failed. Mod folder was not checked.";
                    conflictStatusText.Foreground = mutedTextBrush;
                }
                break;
                case ItemReplaceUiState.Compatible : compatibilityText.Text = itemReplaceUiMessage;
                compatibilityText.Foreground = successBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = "Compatibility passed. Ready to check the active Mod Engine 2 folder.";
                    conflictStatusText.Foreground = warningBrush;
                }
                break;
                case ItemReplaceUiState.NoConflict : compatibilityText.Text = "Compatibility passed.";
                compatibilityText.Foreground = successBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = itemReplaceUiMessage;
                    conflictStatusText.Foreground = successBrush;
                }
                break;
                case ItemReplaceUiState.Conflict : compatibilityText.Text = "Compatibility passed.";
                compatibilityText.Foreground = successBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = itemReplaceUiMessage;
                    conflictStatusText.Foreground = warningBrush;
                }
                if (conflictActionPanel != null) {
                    conflictActionPanel.Visibility = Visibility.Visible;
                }
                break;
                case ItemReplaceUiState.Installed : compatibilityText.Text = "Compatibility passed.";
                compatibilityText.Foreground = successBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = itemReplaceUiMessage;
                    conflictStatusText.Foreground = successBrush;
                }
                break;
                case ItemReplaceUiState.Cancelled : compatibilityText.Text = "Compatibility passed.";
                compatibilityText.Foreground = successBrush;
                if (conflictStatusText != null) {
                    conflictStatusText.Text = itemReplaceUiMessage;
                    conflictStatusText.Foreground = mutedTextBrush;
                }
                break;
            }
            UpdateItemReplaceStepStates();
        }
        private void UpdateItemReplaceStepStates() {
            bool hasSource = detectedModFiles.Count> 0;
            bool hasTarget = targetItemComboBox?.SelectedItem is EldenRingItem;
            if (replacementTargetCard != null) {
                replacementTargetCard.IsEnabled = hasSource;
                replacementTargetCard.Opacity = hasSource? 1.0 : 0.45;
            }
            if (compatibilityCard != null) {
                bool enabled = hasSource && hasTarget;
                compatibilityCard.IsEnabled = enabled;
                compatibilityCard.Opacity = enabled? 1.0 : 0.45;
            }
            bool compatibilityPassed = itemReplaceUiState == ItemReplaceUiState.Compatible || itemReplaceUiState == ItemReplaceUiState.NoConflict || itemReplaceUiState == ItemReplaceUiState.Conflict || itemReplaceUiState == ItemReplaceUiState.Installed || itemReplaceUiState == ItemReplaceUiState.Cancelled;
            if (conflictCard != null) {
                conflictCard.IsEnabled = compatibilityPassed;
                conflictCard.Opacity = compatibilityPassed? 1.0 : 0.45;
            }
            bool readyToInstall = itemReplaceUiState == ItemReplaceUiState.NoConflict;
            if (installCard != null) {
                installCard.IsEnabled = readyToInstall;
                installCard.Opacity = readyToInstall? 1.0 : 0.45;
            }
            if (compatibilityCheckButton != null) {
                compatibilityCheckButton.IsEnabled = hasSource && hasTarget;
            }
            if (conflictCheckButton != null) {
                conflictCheckButton.IsEnabled = compatibilityPassed;
            }
            if (installButton != null) {
                installButton.IsEnabled = readyToInstall;
            }
            if (installSummaryText != null) {
                if (readyToInstall && targetItemComboBox?.SelectedItem is EldenRingItem target) {
                    installSummaryText.Text = "Ready to install. Target mapping: " + BuildCurrentTargetMappingSummary() + ".";
                } else {
                    installSummaryText.Text = "Complete the checks above before installing.";
                }
            }
        }
        private void InstallReplace_Click(object sender, RoutedEventArgs e) {
            if (!EnsureDestinationReady()) return;
            if (targetItemComboBox?.SelectedItem is not EldenRingItem target) {
                SetCompatibilityWarning("Choose a valid target item first.");
                return;
            }
            if (IsMultiArmorTargeting() && GetMissingTargetTypes().Count> 0) {
                SetCompatibilityWarning("Choose all missing target pieces before installing: " + string.Join(", ", GetMissingTargetTypes()));
                return;
            }
            ScanInstalledModFolder();
            if (FindTargetConflictsForCurrentSelection().Count> 0) {
                compatibilityText!.Text = "One or more selected target piece slots are now occupied. Run Check Compatibility again.";
                compatibilityText.Foreground = warningBrush;
                installButton!.IsEnabled = false;
                return;
            }
            int copied = CopySourceFilesToModFolderByCurrentSelection(false);
            compatibilityText!.Text = "Installed successfully. Target mapping: " + BuildCurrentTargetMappingSummary() + ". " + copied + " file(s) copied.";
            compatibilityText.Foreground = successBrush;
            itemReplaceUiState = ItemReplaceUiState.Installed;
            itemReplaceUiMessage = compatibilityText.Text;
            installButton!.IsEnabled = false;
            ScanInstalledModFolder();
            ResetItemReplaceWorkflowAfterSuccessfulInstall();
            ShowToast("Skin installed • " + copied + " file(s)", ToastKind.Success);
        }
        private void ResetItemReplaceWorkflowAfterSuccessfulInstall() {
            itemReplaceSourcePaths.Clear();
            detectedModFiles.Clear();
            itemReplaceTargetCandidates.Clear();
            itemReplacePieceTargets.Clear();
            itemReplaceSourceDisplay = "No mod source selected";
            itemReplaceSourceInfo = "Source: None";
            itemReplaceSelectedTargetFileName = "";
            itemReplaceBaseTargetModelId = "";
            currentRecommendation = null;
            itemReplaceUiState = ItemReplaceUiState.NotChecked;
            itemReplaceUiMessage = "Not checked";
            BuildItemReplacePage();
            if (StatusText != null) StatusText.Text = "Skin > Item Replace";
        }
        private void BuildInstalledModsPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Installed Mods", "See, back up, restore, reveal, or remove skin replacements currently active in your Mod Engine parts folder."));
            Border summaryCard = CreateCard();
            summaryCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel summaryStack = new StackPanel();
            summaryStack.Children.Add(CreateSectionTitle("ACTIVE MOD ENGINE 2 FOLDER"));
            string activeModRoot = GetModEngineRootFolder();
            if (string.IsNullOrWhiteSpace(activeModRoot) ||!Directory.Exists(activeModRoot)) {
                summaryStack.Children.Add(CreateFieldLabel("No valid Mod Engine 2 folder is configured."));
                Button configureButton = new Button {
                    Content = "Open Mod Engine 2 Folder", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0)
                };
                configureButton.Click += (s, e) => {
                    OpenToolPage("Mod Engine 2 Folder");
                };
                summaryStack.Children.Add(configureButton);
                summaryCard.Child = summaryStack;
                root.Children.Add(summaryCard);
                ContentHost.Children.Add(root);
                return;
            }
            EnsureInstalledModFolderScanned();
            summaryStack.Children.Add(CreateInfoLine("Folder", GetModEngineRootFolder()));
            summaryStack.Children.Add(CreateInfoLine("Installed Files", installedModFiles.Count.ToString()));
            int armorSetCount = installedModFiles.Where(x => IsArmorEquipmentType(x.Type)).Select(x => x.ModelId).Distinct().Count();
            int nonArmorCount = installedModFiles.Where(x =>!IsArmorEquipmentType(x.Type)).Count();
            summaryStack.Children.Add(CreateInfoLine("Detected Entries", (armorSetCount + nonArmorCount).ToString()));
            summaryStack.Children.Add(CreateInfoLine("Backup Location", GetInstalledModsBackupRoot()));
            Button refreshButton = new Button {
                Content = "Refresh", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0)
            };
            refreshButton.Click += (s, e) => {
                InvalidateInstalledModSnapshot();
                BuildInstalledModsPage();
            };
            summaryStack.Children.Add(refreshButton);
            summaryCard.Child = summaryStack;
            root.Children.Add(summaryCard);
            Border searchCard = CreateCard();
            searchCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel searchStack = new StackPanel();
            searchStack.Children.Add(CreateSectionTitle("SEARCH INSTALLED MODS"));
            TextBox searchBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Margin = new Thickness(0, 10, 0, 0), ToolTip = "Search by item name, type, model ID, or filename"
            };
            searchStack.Children.Add(searchBox);
            searchCard.Child = searchStack;
            root.Children.Add(searchCard);
            StackPanel listHost = new StackPanel();
            root.Children.Add(listHost);
            List<InstalledModEntry> entries = BuildInstalledModEntries();
            int pageSize = Math.Max(10, installedModsPageSize);
            int visibleCount = pageSize;
            void RenderInstalledEntries(string query) {
                listHost.Children.Clear();
                string normalized = query.Trim();
                List<InstalledModEntry> filtered = entries.Where(entry => string.IsNullOrWhiteSpace(normalized) || entry.SearchText.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToList();
                if (filtered.Count == 0) {
                    Border emptyCard = CreateCard();
                    emptyCard.Child = CreateFieldLabel(string.IsNullOrWhiteSpace(normalized)? "No supported skin replacement files were found in the active Mod Engine 2 folder." : "No installed replacements match your search.");
                    listHost.Children.Add(emptyCard);
                    return;
                }
                int renderCount = Math.Min(visibleCount, filtered.Count);
                for (int index = 0; index<renderCount; index++) {
                    listHost.Children.Add(CreateInstalledModCard(filtered[index]));
                }
                if (renderCount<filtered.Count) {
                    Button loadMoreButton = new Button {
                        Content = "Load More (" + renderCount + " / " + filtered.Count + ")", Style = (Style) FindResource("ActionButton"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 18)
                    };
                    loadMoreButton.Click += (s, e) => {
                        visibleCount += pageSize;
                        RenderInstalledEntries(searchBox.Text);
                    };
                    listHost.Children.Add(loadMoreButton);
                }
            }
            searchBox.TextChanged += (s, e) => {
                visibleCount = pageSize;
                RenderInstalledEntries(searchBox.Text);
            };
            RenderInstalledEntries("");
            ContentHost.Children.Add(root);
        }
        private List<InstalledModEntry> BuildInstalledModEntries() {
            List<InstalledModEntry> result = new List<InstalledModEntry>();
            foreach (IGrouping<string, DetectedModFile> armorGroup in installedModFiles.Where(x => IsArmorEquipmentType(x.Type)).GroupBy(x => x.ModelId).OrderBy(group => int.TryParse(group.Key, out int id)? id : int.MaxValue)) {
                List<InstalledModPiece> pieces = armorGroup.OrderBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => x.FileName).Select(file => new InstalledModPiece {
                    Type = file.Type, ModelId = file.ModelId, FileName = file.FileName, FullPath = file.FullPath, ItemName = FindInstalledItemName(file)
                }).ToList();
                string title;
                if (pieces.Count == 1) {
                    title = pieces[0].ItemName;
                } else {
                    title = "Armor Set / Model " + armorGroup.Key;
                }
                InstalledModEntry entry = new InstalledModEntry {
                    Title = title, Subtitle = "Armor" + "  •  Model ID " + armorGroup.Key + "  •  " + pieces.Count + (pieces.Count == 1? " file" : " files"), Pieces = pieces
                };
                entry.SearchText = BuildInstalledSearchText(entry);
                result.Add(entry);
            }
            foreach (DetectedModFile file in installedModFiles.Where(x =>!IsArmorEquipmentType(x.Type)).OrderBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => int.TryParse(x.ModelId, out int id)? id : int.MaxValue).ThenBy(x => x.FileName)) {
                InstalledModPiece piece = new InstalledModPiece {
                    Type = file.Type, ModelId = file.ModelId, FileName = file.FileName, FullPath = file.FullPath, ItemName = FindInstalledItemName(file)
                };
                InstalledModEntry entry = new InstalledModEntry {
                    Title = piece.ItemName, Subtitle = piece.Type + "  •  Model ID " + piece.ModelId, Pieces = new List<InstalledModPiece> {
                        piece
                    }
                };
                entry.SearchText = BuildInstalledSearchText(entry);
                result.Add(entry);
            }
            return result;
        }
        private Border CreateInstalledModCard(InstalledModEntry entry) {
            Border card = CreateCard();
            card.Margin = new Thickness(0, 0, 0, 12);
            StackPanel stack = new StackPanel();
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            StackPanel titleStack = new StackPanel();
            TextBlock title = new TextBlock {
                Text = entry.Title, Foreground = normalTextBrush, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
            };
            TextBlock subtitle = new TextBlock {
                Text = entry.Subtitle, Foreground = mutedTextBrush, FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0)
            };
            bool databaseMatched = entry.Pieces.All(x =>!x.ItemName.Equals("(Unknown database item)", StringComparison.OrdinalIgnoreCase));
            TextBlock status = new TextBlock {
                Text = databaseMatched? "ACTIVE  •  DATABASE MATCHED" : "ACTIVE  •  UNKNOWN DATABASE ITEM", Foreground = databaseMatched? successBrush : warningBrush, FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0)
            };
            titleStack.Children.Add(title);
            titleStack.Children.Add(subtitle);
            titleStack.Children.Add(status);
            headerGrid.Children.Add(titleStack);
            Button revealButton = new Button {
                Content = "Open File Location", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top
            };
            revealButton.Click += (s, e) => {
                RevealInstalledEntry(entry);
            };
            Grid.SetColumn(revealButton, 1);
            headerGrid.Children.Add(revealButton);
            stack.Children.Add(headerGrid);
            foreach (InstalledModPiece piece in entry.Pieces) {
                string line = "• " + piece.Type + ": " + piece.ItemName + "\n   " + piece.FileName;
                stack.Children.Add(new TextBlock {
                    Text = line, Foreground = normalTextBrush, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
                });
            }
            StackPanel actionPanel = new StackPanel {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0)
            };
            Button backupButton = new Button {
                Content = "Backup", Style = (Style) FindResource("ActionButton")
            };
            backupButton.Click += (s, e) => {
                BackupInstalledEntryWithPopup(entry);
            };
            Button restoreButton = new Button {
                Content = "Restore Latest", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(8, 0, 0, 0)
            };
            restoreButton.Click += (s, e) => {
                RestoreLatestInstalledEntryBackup(entry);
            };
            Button removeButton = new Button {
                Content = "Remove", Style = (Style) FindResource("ActionButton"), Foreground = errorBrush, Margin = new Thickness(8, 0, 0, 0)
            };
            removeButton.Click += (s, e) => {
                RemoveInstalledEntry(entry);
            };
            actionPanel.Children.Add(backupButton);
            actionPanel.Children.Add(restoreButton);
            actionPanel.Children.Add(removeButton);
            stack.Children.Add(actionPanel);
            card.Child = stack;
            return card;
        }
        private string GetInstalledModsBackupRoot() {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager", "Backups", "InstalledMods");
            Directory.CreateDirectory(root);
            return root;
        }
        private string GetInstalledEntryBackupKey(InstalledModEntry entry) {
            string modelId = entry.Pieces.FirstOrDefault()?.ModelId ?? "Unknown";
            string type = entry.Pieces.FirstOrDefault()?.Type ?? "Unknown";
            string raw = type + "_" + modelId;
            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                raw = raw.Replace(invalid, '_');
            }
            return raw;
        }
        private string GetInstalledEntryBackupFolder(InstalledModEntry entry) {
            return Path.Combine(GetInstalledModsBackupRoot(), GetInstalledEntryBackupKey(entry));
        }
        private bool TryBackupInstalledEntry(InstalledModEntry entry, out string snapshotFolder, out string error) {
            snapshotFolder = "";
            error = "";
            try {
                List<InstalledModPiece> existingPieces = entry.Pieces.Where(piece => File.Exists(piece.FullPath)).ToList();
                if (existingPieces.Count == 0) {
                    error = "No installed files were found to back up.";
                    return false;
                }
                string entryFolder = GetInstalledEntryBackupFolder(entry);
                snapshotFolder = Path.Combine(entryFolder, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(snapshotFolder);
                foreach (InstalledModPiece piece in existingPieces) {
                    string relativePath;
                    try {
                        relativePath = Path.GetRelativePath(GetModEngineRootFolder(), piece.FullPath);
                    } catch {
                        relativePath = piece.FileName;
                    }
                    if (relativePath.StartsWith("..")) {
                        relativePath = piece.FileName;
                    }
                    string backupPath = Path.Combine(snapshotFolder, relativePath);
                    string?backupDirectory = Path.GetDirectoryName(backupPath);
                    if (!string.IsNullOrWhiteSpace(backupDirectory)) {
                        Directory.CreateDirectory(backupDirectory);
                    }
                    File.Copy(piece.FullPath, backupPath, true);
                }
                string infoPath = Path.Combine(snapshotFolder, "backup_info.txt");
                File.WriteAllText(infoPath, "Elden Ring Toolkit Installed Mods Backup" + Environment.NewLine + "Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + "Entry: " + entry.Title + Environment.NewLine + "Files: " + existingPieces.Count + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, existingPieces.Select(x => x.FullPath)));
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }
        private void BackupInstalledEntryWithPopup(InstalledModEntry entry) {
            if (TryBackupInstalledEntry(entry, out string snapshotFolder, out string error)) {
                MessageBox.Show("Backup created successfully." + "\n\n" + "Entry: " + entry.Title + "\n" + "Files: " + entry.Pieces.Count + "\n\n" + snapshotFolder, "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "Skin > Installed Mods > Backup created";
            } else {
                MessageBox.Show("Backup could not be created." + "\n\n" + error, "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RestoreLatestInstalledEntryBackup(InstalledModEntry entry) {
            try {
                string entryFolder = GetInstalledEntryBackupFolder(entry);
                if (!Directory.Exists(entryFolder)) {
                    MessageBox.Show("No backup exists for this entry yet.", "No Backup Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string?latestSnapshot = Directory.GetDirectories(entryFolder).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(latestSnapshot)) {
                    MessageBox.Show("No backup exists for this entry yet.", "No Backup Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string[] backupFiles = Directory.GetFiles(latestSnapshot, "*.partsbnd.dcx", SearchOption.AllDirectories);
                if (backupFiles.Length == 0) {
                    MessageBox.Show("The latest backup contains no .partsbnd.dcx files.", "Backup Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                MessageBoxResult result = MessageBox.Show("Restore the latest backup for:" + "\n\n" + entry.Title + "\n\n" + "This will overwrite matching files in the active Mod Engine 2 folder.", "Restore Latest Backup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) {
                    return;
                }
                int restored = 0;
                foreach (string backupFile in backupFiles) {
                    string relativePath = Path.GetRelativePath(latestSnapshot, backupFile);
                    string destination = Path.Combine(GetModEngineRootFolder(), relativePath);
                    string?destinationDirectory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory)) {
                        Directory.CreateDirectory(destinationDirectory);
                    }
                    File.Copy(backupFile, destination, true);
                    restored++;
                }
                MessageBox.Show("Latest backup restored successfully." + "\n\n" + "Entry: " + entry.Title + "\n" + "Files restored: " + restored, "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "Skin > Installed Mods > Backup restored";
                InvalidateInstalledModSnapshot();
                BuildInstalledModsPage();
            } catch (Exception ex) {
                MessageBox.Show("The backup could not be restored." + "\n\n" + ex.Message, "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RemoveInstalledEntry(InstalledModEntry entry) {
            List<InstalledModPiece> existingPieces = entry.Pieces.Where(piece => File.Exists(piece.FullPath)).ToList();
            if (existingPieces.Count == 0) {
                MessageBox.Show("These files are no longer present in the active Mod Engine 2 folder.", "Nothing To Remove", MessageBoxButton.OK, MessageBoxImage.Information);
                BuildInstalledModsPage();
                return;
            }
            MessageBoxResult result = MessageBox.Show("Remove this installed skin replacement?" + "\n\n" + entry.Title + "\n" + "Files to remove: " + existingPieces.Count + "\n\n" + "A backup will be created automatically before deletion.", "Remove Installed Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) {
                return;
            }
            if (!TryBackupInstalledEntry(entry, out string snapshotFolder, out string backupError)) {
                MessageBox.Show("The mod was NOT removed because its safety backup failed." + "\n\n" + backupError, "Remove Cancelled", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try {
                int removed = 0;
                foreach (InstalledModPiece piece in existingPieces) {
                    if (File.Exists(piece.FullPath)) {
                        File.Delete(piece.FullPath);
                        removed++;
                    }
                }
                MessageBox.Show("Installed skin replacement removed." + "\n\n" + "Entry: " + entry.Title + "\n" + "Files removed: " + removed + "\n\n" + "Safety backup:" + "\n" + snapshotFolder, "Remove Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "Skin > Installed Mods > Removed";
                InvalidateInstalledModSnapshot();
                BuildInstalledModsPage();
            } catch (Exception ex) {
                MessageBox.Show("The backup was created, but one or more files could not be removed." + "\n\n" + ex.Message, "Remove Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RevealInstalledEntry(InstalledModEntry entry) {
            List<string> existingFiles = entry.Pieces.Select(x => x.FullPath).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (existingFiles.Count == 0) {
                MessageBox.Show("The installed files could not be found.", "Files Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try {
                string?commonFolder = Path.GetDirectoryName(existingFiles[0]);
                bool sameFolder =!string.IsNullOrWhiteSpace(commonFolder) && existingFiles.All(file => string.Equals(Path.GetDirectoryName(file), commonFolder, StringComparison.OrdinalIgnoreCase));
                if (sameFolder && OpenFolderAndSelectItems(commonFolder!, existingFiles)) {
                    StatusText.Text = "Skin > Installed Mods > File location opened";
                    return;
                }
                Process.Start(new ProcessStartInfo {
                    FileName = "explorer.exe", Arguments = "/select,\"" + existingFiles[0] + "\"", UseShellExecute = true
                });
                StatusText.Text = "Skin > Installed Mods > File location opened";
            } catch (Exception ex) {
                MessageBox.Show("Could not open File Explorer." + "\n\n" + ex.Message, "Open File Location Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static bool OpenFolderAndSelectItems(string folderPath, IReadOnlyList<string> filePaths) {
            IntPtr folderPidl = IntPtr.Zero;
            List<IntPtr> itemPidls = new List<IntPtr>();
            try {
                folderPidl = ILCreateFromPathW(folderPath);
                if (folderPidl == IntPtr.Zero) {
                    return false;
                }
                foreach (string filePath in filePaths) {
                    IntPtr itemPidl = ILCreateFromPathW(filePath);
                    if (itemPidl != IntPtr.Zero) {
                        itemPidls.Add(itemPidl);
                    }
                }
                if (itemPidls.Count == 0) {
                    return false;
                }
                int result = SHOpenFolderAndSelectItems(folderPidl, (uint) itemPidls.Count, itemPidls.ToArray(), 0);
                return result >= 0;
            } finally {
                foreach (IntPtr itemPidl in itemPidls) {
                    if (itemPidl != IntPtr.Zero) {
                        ILFree(itemPidl);
                    }
                }
                if (folderPidl != IntPtr.Zero) {
                    ILFree(folderPidl);
                }
            }
        }
        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true
        )] private static extern IntPtr ILCreateFromPathW(string pszPath);
        [DllImport(
            "shell32.dll",
            SetLastError = true
        )] private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [MarshalAs(
                UnmanagedType.LPArray,
                ArraySubType = UnmanagedType.SysInt
            )] IntPtr[] apidl, uint dwFlags);
        [DllImport(
            "shell32.dll"
        )] private static extern void ILFree(IntPtr pidl);
        private string FindInstalledItemName(DetectedModFile file) {
            EldenRingItem? exact = itemDatabase.FirstOrDefault(x => x.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) {
                return exact.Name;
            }
            EldenRingItem? bySlot = itemDatabase.FirstOrDefault(x => x.Type == file.Type && x.ModelId == file.ModelId);
            if (bySlot != null) {
                return bySlot.Name;
            }
            return "(Unknown database item)";
        }
        private static bool IsArmorEquipmentType(string type) {
            return type == "Helmet" || type == "Chest Armor" || type == "Gauntlet" || type == "Greaves";
        }
        private static string BuildInstalledSearchText(InstalledModEntry entry) {
            IEnumerable<string> values = new[] {
                entry.Title, entry.Subtitle
            }
            .Concat(entry.Pieces.SelectMany(piece => new[] {
                piece.ItemName, piece.Type, piece.ModelId, piece.FileName, piece.FullPath
            }));
            return string.Join(" ", values);
        }
        private void BuildModFolderPage() {
            ContentHost.Children.Clear();
            StackPanel root = new StackPanel();
            root.Children.Add(CreatePageTitle("Mod Engine 2 Folder", "Manage the Mod Engine 2 workspace used by Skin tools. The toolkit detects the parts folder and installed replacement files automatically."));
            string modRoot = GetModEngineRootFolder();
            string partsFolder = GetPartsDestinationFolder(false);
            Border folderCard = CreateCard();
            folderCard.Margin = new Thickness(0, 0, 0, 16);
            StackPanel folderStack = new StackPanel();
            folderStack.Children.Add(CreateSectionTitle("ACTIVE MOD ENGINE 2 WORKSPACE"));
            folderStack.Children.Add(CreateFieldLabel("Select the Mod Engine 2 mod root, for example ...\\Game\\mod. " + "Skin tools automatically use its parts subfolder, while other mod content in the workspace is left untouched."));
            Grid row = new Grid {
                Margin = new Thickness(0, 12, 0, 0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star)
            });
            row.ColumnDefinitions.Add(new ColumnDefinition {
                Width = GridLength.Auto
            });
            TextBox pathBox = new TextBox {
                Style = (Style) FindResource("DarkTextBox"), Text = string.IsNullOrWhiteSpace(modRoot)? "No Mod Engine 2 folder configured" : modRoot, IsReadOnly = true
            };
            row.Children.Add(pathBox);
            Button browse = new Button {
                Content = "Set Mod Engine 2 Folder...", Style = (Style) FindResource("ActionButton"), Margin = new Thickness(10, 0, 0, 0)
            };
            browse.Click += (_, _) => {
                ChooseActiveModFolder();
                BuildModFolderPage();
            };
            Grid.SetColumn(browse, 1);
            row.Children.Add(browse);
            folderStack.Children.Add(row);
            if (!string.IsNullOrWhiteSpace(modRoot) && Directory.Exists(modRoot)) {
                folderStack.Children.Add(CreateInfoLine("Parts Folder", Directory.Exists(partsFolder)? partsFolder : "Not present yet — Skin Item Replace will create it when needed"));
                string[] knownContentFolders = {
                    "action", "asset", "chr", "event", "map", "material", "menu", "msg", "param", "parts", "script", "sfx"
                };
                string[] detectedFolders = knownContentFolders.Where(name => Directory.Exists(Path.Combine(modRoot, name))).ToArray();
                folderStack.Children.Add(CreateInfoLine("Detected Content", detectedFolders.Length == 0? "No known mod subfolders detected" : string.Join(", ", detectedFolders)));
            }
            StackPanel actionRow = new StackPanel {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0)
            };
            Button openFolderButton = new Button {
                Content = "Open Folder", Style = (Style) FindResource("ActionButton"), IsEnabled =!string.IsNullOrWhiteSpace(modRoot) && Directory.Exists(modRoot)
            };
            openFolderButton.Click += (_, _) => {
                OpenFolderInExplorer(modRoot);
            };
            actionRow.Children.Add(openFolderButton);
            folderStack.Children.Add(actionRow);
            folderCard.Child = folderStack;
            root.Children.Add(folderCard);
            Border scanCard = CreateCard();
            StackPanel scanStack = new StackPanel();
            scanStack.Children.Add(CreateSectionTitle("INSTALLED SKIN REPLACEMENTS"));
            if (string.IsNullOrWhiteSpace(modRoot) ||!Directory.Exists(modRoot)) {
                scanStack.Children.Add(CreateFieldLabel("Configure a valid Mod Engine 2 folder first."));
            } else if (string.IsNullOrWhiteSpace(partsFolder) ||!Directory.Exists(partsFolder)) {
                scanStack.Children.Add(CreateFieldLabel("No parts folder is present yet. This is normal if the workspace does not currently contain skin replacements."));
            } else {
                ScanInstalledModFolder();
                scanStack.Children.Add(CreateInfoLine("Parts Folder", partsFolder));
                scanStack.Children.Add(CreateInfoLine("Detected Files", installedModFiles.Count.ToString()));
                foreach (DetectedModFile file in installedModFiles.OrderBy(x => GetTypeSortOrder(x.Type)).ThenBy(x => x.ModelId).ThenBy(x => x.FileName)) {
                    string item = FindItemDisplayName(file.Type, file.ModelId);
                    scanStack.Children.Add(new TextBlock {
                        Text = "• " + file.FileName + "   [" + file.Type + " / " + file.ModelId + "]   " + item, Foreground = normalTextBrush, FontSize = 12.5, Margin = new Thickness(0, 9, 0, 0), TextWrapping = TextWrapping.Wrap
                    });
                }
            }
            scanCard.Child = scanStack;
            root.Children.Add(scanCard);
            ContentHost.Children.Add(root);
        }
        private void LoadSavedModFolder() {
            try {
                if (File.Exists(SettingsFilePath)) {
                    string savedPath = File.ReadAllText(SettingsFilePath).Trim();
                    gameModFolder = NormalizeModEngineRootFolder(savedPath);
                    if (!string.Equals(savedPath, gameModFolder, StringComparison.OrdinalIgnoreCase)) {
                        SaveModFolder();
                    }
                }
            } catch {
                gameModFolder = "";
            }
        }
        private void SaveModFolder() {
            gameModFolder = NormalizeModEngineRootFolder(gameModFolder);
            Directory.CreateDirectory(settingsFolder);
            File.WriteAllText(SettingsFilePath, gameModFolder);
        }
        private string GetPartsDestinationFolder(bool createIfMissing) {
            string modRoot = GetModEngineRootFolder();
            if (string.IsNullOrWhiteSpace(modRoot)) {
                return "";
            }
            string parts = Path.Combine(modRoot, "parts");
            if (Directory.Exists(parts)) {
                return parts;
            }
            if (createIfMissing) {
                Directory.CreateDirectory(parts);
            }
            return parts;
        }
        private void ScanInstalledModFolder() {
            installedModFiles.Clear();
            string partsFolder = GetPartsDestinationFolder(false);
            if (string.IsNullOrWhiteSpace(partsFolder) ||!Directory.Exists(partsFolder)) {
                installedModSnapshotValid = true;
                return;
            }
            string[] files;
            try {
                files = Directory.GetFiles(partsFolder, "*.partsbnd.dcx", SearchOption.AllDirectories);
            } catch {
                files = Array.Empty<string>();
            }
            foreach (string fullPath in files) {
                string fileName = Path.GetFileName(fullPath);
                ParsedPartFile parsed = ParsePartFileName(fileName);
                if (parsed.Type == "Unknown") {
                    continue;
                }
                installedModFiles.Add(new DetectedModFile {
                    FullPath = fullPath, FileName = fileName, Type = parsed.Type, ModelId = parsed.ModelId
                });
            }
            installedModSnapshotValid = true;
        }
        private void EnsureInstalledModFolderScanned() {
            if (!installedModSnapshotValid) {
                ScanInstalledModFolder();
            }
        }
        private void InvalidateInstalledModSnapshot() {
            installedModSnapshotValid = false;
        }
        private static string NormalizeLowResolutionPartFileName(string fileName) {
            const string extension = ".partsbnd.dcx";
            int ext = fileName.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (ext<0) return fileName;
            string stem = fileName.Substring(0, ext);
            string suffix = fileName.Substring(ext);
            if (stem.EndsWith("_l", StringComparison.OrdinalIgnoreCase) || stem.EndsWith(".l", StringComparison.OrdinalIgnoreCase)) {
                stem = stem.Substring(0, stem.Length - 2);
            }
            return stem + suffix;
        }
        private bool HasDuplicateLogicalSourceTypes() {
            return detectedModFiles.GroupBy(x => x.Type, StringComparer.OrdinalIgnoreCase).Any(group => group.Select(x => x.ModelId).Distinct(StringComparer.OrdinalIgnoreCase).Count()> 1);
        }
        private ParsedPartFile ParsePartFileName(string fileName) {
            itemDatabaseByFileName.TryGetValue(fileName, out EldenRingItem? databaseItem);
            if (databaseItem == null) {
                string canonicalFileName = NormalizeLowResolutionPartFileName(fileName);
                if (!canonicalFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) {
                    itemDatabaseByFileName.TryGetValue(canonicalFileName, out databaseItem);
                }
            }
            if (databaseItem != null) {
                return new ParsedPartFile {
                    Type = databaseItem.Type, ModelId = databaseItem.ModelId
                };
            }
            string type = EldenRingDatabase.DetectArmorType(fileName);
            if (type == "Unknown") {
                return new ParsedPartFile {
                    Type = "Unknown", ModelId = ""
                };
            }
            int ext = fileName.IndexOf(".partsbnd.dcx", StringComparison.OrdinalIgnoreCase);
            string stem = ext >= 0? fileName.Substring(0, ext) : fileName;
            string[] parts = stem.Split('_');
            return new ParsedPartFile {
                Type = type, ModelId = parts.Length >= 3? parts[2] : ""
            };
        }
        private List<FileConflict> FindSourceConflicts() {
            List<FileConflict> result = new List<FileConflict>();
            foreach (DetectedModFile source in detectedModFiles) {
                DetectedModFile? existing = installedModFiles.FirstOrDefault(x => x.Type == source.Type && x.ModelId == source.ModelId);
                if (existing != null) result.Add(new FileConflict {
                    Source = source, Existing = existing
                });
            }
            return result;
        }
        private List<FileConflict> FindTargetConflictsForCurrentSelection() {
            List<FileConflict> result = new List<FileConflict>();
            foreach (DetectedModFile source in detectedModFiles) {
                EldenRingItem? target = GetEffectiveTargetForSource(source);
                if (target == null) continue;
                DetectedModFile? existing = installedModFiles.FirstOrDefault(x => x.Type.Equals(source.Type, StringComparison.OrdinalIgnoreCase) && x.ModelId.Equals(target.ModelId, StringComparison.OrdinalIgnoreCase));
                if (existing != null) result.Add(new FileConflict {
                    Source = source, Existing = existing
                });
            }
            return result;
        }
        private List<FileConflict> FindTargetConflicts(string targetModelId) {
            List<FileConflict> result = new List<FileConflict>();
            foreach (DetectedModFile source in detectedModFiles) {
                DetectedModFile? existing = installedModFiles.FirstOrDefault(x => x.Type == source.Type && x.ModelId == targetModelId);
                if (existing != null) {
                    result.Add(new FileConflict {
                        Source = source, Existing = existing
                    });
                }
            }
            return result;
        }
        private ConflictRecommendation? BuildRecommendation() {
            HashSet<string> sourceTypes = detectedModFiles.Select(x => x.Type).ToHashSet();
            if (sourceTypes.Count == 0) {
                return null;
            }
            List<string> candidateIds = itemDatabase.Where(x => sourceTypes.Contains(x.Type)).Select(x => x.ModelId).Distinct().OrderBy(x => int.TryParse(x, out int n)? n : int.MaxValue).ToList();
            foreach (string id in candidateIds) {
                bool validForAll = sourceTypes.All(type => itemDatabase.Any(x => x.Type == type && x.ModelId == id));
                if (!validForAll) {
                    continue;
                }
                bool freeForAll = sourceTypes.All(type =>!installedModFiles.Any(x => x.Type == type && x.ModelId == id));
                if (!freeForAll) {
                    continue;
                }
                string description = string.Join(" | ", sourceTypes.OrderBy(GetTypeSortOrder).Select(type => type + ": " + FindItemDisplayName(type, id)));
                return new ConflictRecommendation {
                    ModelId = id, Description = description
                };
            }
            return null;
        }
        private string RewriteModelId(string fileName, string newModelId) {
            int ext = fileName.IndexOf(".partsbnd.dcx", StringComparison.OrdinalIgnoreCase);
            if (ext<0) return fileName;
            string stem = fileName.Substring(0, ext);
            string extension = fileName.Substring(ext);
            string[] parts = stem.Split('_');
            if (parts.Length<3) return fileName;
            parts[2] = newModelId;
            return string.Join("_", parts) + extension;
        }
        private bool EnsureDestinationReady() {
            string modRoot = GetModEngineRootFolder();
            if (string.IsNullOrWhiteSpace(modRoot) ||!Directory.Exists(modRoot)) {
                MessageBox.Show("Configure a valid Mod Engine 2 folder under Skin > Mod Engine 2 Folder first.", "Elden Ring Toolkit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (detectedModFiles.Count == 0) {
                MessageBox.Show("Import a new skin mod source first.", "Elden Ring Toolkit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }
        private int CopySourceFilesToModFolderByCurrentSelection(bool overwrite) {
            string destinationFolder = GetPartsDestinationFolder(true);
            int copied = 0;
            foreach (DetectedModFile source in detectedModFiles) {
                EldenRingItem? target = GetEffectiveTargetForSource(source);
                if (target == null) continue;
                string destName = RewriteModelId(source.FileName, target.ModelId);
                string dest = Path.Combine(destinationFolder, destName);
                if (File.Exists(dest) &&!overwrite) continue;
                File.Copy(source.FullPath, dest, overwrite);
                copied++;
            }
            return copied;
        }
        private void BackupAndRemoveCurrentTargetSlots() {
            string backupRoot = Path.Combine(GetModEngineRootFolder(), "_EldenRingToolkit_Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            foreach (DetectedModFile source in detectedModFiles) {
                EldenRingItem? target = GetEffectiveTargetForSource(source);
                if (target == null) continue;
                foreach (DetectedModFile existing in installedModFiles.Where(x => x.Type.Equals(source.Type, StringComparison.OrdinalIgnoreCase) && x.ModelId.Equals(target.ModelId, StringComparison.OrdinalIgnoreCase)).ToList()) {
                    Directory.CreateDirectory(backupRoot);
                    string backupPath = Path.Combine(backupRoot, existing.FileName);
                    File.Copy(existing.FullPath, backupPath, true);
                    File.Delete(existing.FullPath);
                }
            }
        }
        private int CopySourceFilesToModFolder(string?replacementModelId, bool overwrite) {
            string destinationFolder = GetPartsDestinationFolder(true);
            int copied = 0;
            foreach (DetectedModFile source in detectedModFiles) {
                string destName = replacementModelId == null? source.FileName : RewriteModelId(source.FileName, replacementModelId);
                string dest = Path.Combine(destinationFolder, destName);
                if (File.Exists(dest) &&!overwrite) continue;
                File.Copy(source.FullPath, dest, overwrite);
                copied++;
            }
            return copied;
        }
        private void BackupAndRemoveTargetSlots(string targetModelId) {
            string backupRoot = Path.Combine(GetModEngineRootFolder(), "_EldenRingToolkit_Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            HashSet<string> sourceTypes = detectedModFiles.Select(x => x.Type).ToHashSet();
            foreach (DetectedModFile existing in installedModFiles.Where(x => x.ModelId == targetModelId && sourceTypes.Contains(x.Type)).ToList()) {
                Directory.CreateDirectory(backupRoot);
                string backupPath = Path.Combine(backupRoot, existing.FileName);
                File.Copy(existing.FullPath, backupPath, true);
                File.Delete(existing.FullPath);
            }
        }
        private void OverwriteExisting_Click(object sender, RoutedEventArgs e) {
            if (!EnsureDestinationReady()) return;
            ScanInstalledModFolder();
            MessageBoxResult result = MessageBox.Show("Existing skin files in the selected occupied piece slots will be backed up and replaced.\n\nContinue?", "Overwrite Existing Skin", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            if (targetItemComboBox?.SelectedItem is not EldenRingItem) return;
            BackupAndRemoveCurrentTargetSlots();
            int copied = CopySourceFilesToModFolderByCurrentSelection(true);
            itemReplaceUiState = ItemReplaceUiState.Installed;
            itemReplaceUiMessage = "Overwrite completed. Backup created. Target mapping: " + BuildCurrentTargetMappingSummary() + ". " + copied + " file(s) installed.";
            compatibilityText!.Text = itemReplaceUiMessage;
            compatibilityText.Foreground = successBrush;
            conflictActionPanel!.Visibility = Visibility.Collapsed;
            installButton!.IsEnabled = false;
            ScanInstalledModFolder();
            ResetItemReplaceWorkflowAfterSuccessfulInstall();
            ShowToast("Skin installed • " + copied + " file(s) • backup created", ToastKind.Success);
        }
        private void CancelConflict_Click(object sender, RoutedEventArgs e) {
            itemReplaceUiState = ItemReplaceUiState.Cancelled;
            itemReplaceUiMessage = "Cancelled. No files were changed.";
            compatibilityText!.Text = itemReplaceUiMessage;
            compatibilityText.Foreground = mutedTextBrush;
            conflictActionPanel!.Visibility = Visibility.Collapsed;
            installButton!.IsEnabled = false;
        }
        private void UseRecommendedSlot_Click(object sender, RoutedEventArgs e) {
            if (currentRecommendation == null) {
                MessageBox.Show("No free recommended slot is available in the current database.");
                return;
            }
            if (!EnsureDestinationReady()) return;
            MessageBoxResult result = MessageBox.Show("Rename the imported model ID to " + currentRecommendation.ModelId + " and install it into that free slot?\n\n" + currentRecommendation.Description, "Use Recommended Slot", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            int copied = CopySourceFilesToModFolder(currentRecommendation.ModelId, false);
            string installedModelId = currentRecommendation.ModelId;
            itemReplaceUiState = ItemReplaceUiState.Installed;
            itemReplaceUiMessage = "Installed to recommended free model ID " + installedModelId + ". " + copied + " file(s) copied.";
            compatibilityText!.Text = itemReplaceUiMessage;
            compatibilityText.Foreground = successBrush;
            conflictActionPanel!.Visibility = Visibility.Collapsed;
            installButton!.IsEnabled = false;
            ScanInstalledModFolder();
            ResetItemReplaceWorkflowAfterSuccessfulInstall();
            ShowToast("Skin installed • " + copied + " file(s) • model ID " + installedModelId, ToastKind.Success);
        }
        private string FindItemDisplayName(string type, string modelId) {
            EldenRingItem? item = itemDatabase.FirstOrDefault(x => x.Type == type && x.ModelId == modelId);
            return item?.Name ?? "(not in current database)";
        }
        private Border CreateCard() {
            return new Border {
                Style = (Style) FindResource("CardBorder")
            };
        }
        private StackPanel CreatePageTitle(string title, string description) {
            StackPanel stack = new StackPanel {
                Margin = new Thickness(0, 0, 0, 24)
            };
            stack.Children.Add(new TextBlock {
                Text = title, Foreground = GetResourceBrush("TextMain"), FontFamily = new FontFamily("Georgia"), FontSize = 28, FontWeight = FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock {
                Text = description, Foreground = GetResourceBrush("TextSoft"), FontSize = 13, Margin = new Thickness(0, 7, 0, 0)
            });
            return stack;
        }
        private TextBlock CreateSectionTitle(string text) {
            return new TextBlock {
                Text = text, Foreground = GetResourceBrush("TextMuted"), FontSize = 11, FontWeight = FontWeights.Bold
            };
        }
        private TextBlock CreateFieldLabel(string text) {
            return new TextBlock {
                Text = text, Foreground = GetResourceBrush("TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0)
            };
        }
        private TextBlock CreateInfoLine(string label, string value) {
            return new TextBlock {
                Text = label + ": " + value, Foreground = GetResourceBrush("TextMain"), FontSize = 12.5, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
            };
        }
        private class AshOfWarSelection {
            public bool Accepted {
                get;
                set;
            }
            public InventoryCatalogItem? AshOfWar {
                get;
                set;
            }
        }
        private class InventoryCatalogItem {
            public uint ParamId {
                get;
                set;
            }
            public uint BaseParamId {
                get;
                set;
            }
            public string Name {
                get;
                set;
            }
            = "";
            public string Category {
                get;
                set;
            }
            = "";
            public bool SupportsAffinity {
                get;
                set;
            }
            public bool SupportsAshOfWar {
                get;
                set;
            }
            public string WeaponTypeColumn {
                get;
                set;
            }
            = "";
            public string CompatibleWeaponTypes {
                get;
                set;
            }
            = "";
            public int MaxUpgrade {
                get;
                set;
            }
            = 25;
            public int Affinity {
                get;
                set;
            }
            public int Upgrade {
                get;
                set;
            }
            public override string ToString() => Name;
        }
        private class GaItemInfo {
            public int TableIndex {
                get;
                set;
            }
            public int Offset {
                get;
                set;
            }
            public int Size {
                get;
                set;
            }
            public uint Handle {
                get;
                set;
            }
            public uint ItemId {
                get;
                set;
            }
            public uint GemHandle {
                get;
                set;
            }
            public bool IsEmpty => Handle == 0;
        }
        private class InventoryEntryInfo {
            public int Offset {
                get;
                set;
            }
            public int ArrayIndex {
                get;
                set;
            }
            public bool IsKeyArray {
                get;
                set;
            }
            public string Location {
                get;
                set;
            }
            = "Held";
            public uint Handle {
                get;
                set;
            }
            public uint Quantity {
                get;
                set;
            }
            public uint Index {
                get;
                set;
            }
            public uint ItemId {
                get;
                set;
            }
            public uint ParamId {
                get;
                set;
            }
            public string Category {
                get;
                set;
            }
            = "";
            public string Name {
                get;
                set;
            }
            = "";
            public GaItemInfo? GaItem {
                get;
                set;
            }
            public int Upgrade {
                get;
                set;
            }
            public int Affinity {
                get;
                set;
            }
            public bool SupportsAffinity {
                get;
                set;
            }
            public bool SupportsAshOfWar {
                get;
                set;
            }
            public uint AshOfWarHandle {
                get;
                set;
            }
            public uint AshOfWarParamId {
                get;
                set;
            }
            public string AshOfWarName {
                get;
                set;
            }
            = "None";
            public int MaxUpgrade {
                get;
                set;
            }
            = 25;
        }
        private class InventorySnapshot {
            public int SlotIndex {
                get;
                set;
            }
            public int SlotStart {
                get;
                set;
            }
            public int SlotEnd {
                get;
                set;
            }
            public int GaItemsStart {
                get;
                set;
            }
            public int GaItemsEnd {
                get;
                set;
            }
            public int HeldOffset {
                get;
                set;
            }
            public int StorageOffset {
                get;
                set;
            }
            public List<GaItemInfo> GaItems {
                get;
                set;
            }
            = new List<GaItemInfo>();
            public Dictionary<uint, GaItemInfo> GaItemByHandle {
                get;
                set;
            }
            = new Dictionary<uint, GaItemInfo>();
            public List<InventoryEntryInfo> Items {
                get;
                set;
            }
            = new List<InventoryEntryInfo>();
        }
        private enum ItemReplaceUiState {
            NotChecked, Warning, Compatible, NoConflict, Conflict, Installed, Cancelled
        }
        private class DetectedModFile {
            public string FullPath {
                get;
                set;
            }
            = "";
            public string FileName {
                get;
                set;
            }
            = "";
            public string Type {
                get;
                set;
            }
            = "";
            public string ModelId {
                get;
                set;
            }
            = "";
        }
        private class InstalledModEntry {
            public string Title {
                get;
                set;
            }
            = "";
            public string Subtitle {
                get;
                set;
            }
            = "";
            public string SearchText {
                get;
                set;
            }
            = "";
            public List<InstalledModPiece> Pieces {
                get;
                set;
            }
            = new List<InstalledModPiece>();
        }
        private enum ToastKind {
            Success, Info, Warning
        }
        private class SaveBackupInfo {
            public string FullPath {
                get;
                set;
            }
            = "";
            public DateTime Created {
                get;
                set;
            }
            public string Operation {
                get;
                set;
            }
            = "backup";
        }
        private class AppearancePresetFileData {
            public string Format {
                get;
                set;
            }
            = "EldenRingToolkitAppearancePreset";
            public int Version {
                get;
                set;
            }
            = 1;
            public string Name {
                get;
                set;
            }
            = "Appearance Preset";
            public DateTime CreatedUtc {
                get;
                set;
            }
            public byte BodyType {
                get;
                set;
            }
            public string DataBase64 {
                get;
                set;
            }
            = "";
        }
        private class AppearancePresetPackFileData {
            public string Format {
                get;
                set;
            }
            = "EldenRingToolkitAppearancePresetPack";
            public int Version {
                get;
                set;
            }
            = 1;
            public string Name {
                get;
                set;
            }
            = "Full Appearance Presets - 15 Slots";
            public DateTime CreatedUtc {
                get;
                set;
            }
            public int SlotCount {
                get;
                set;
            }
            = MirrorPresetSlotCount;
            public int SlotSize {
                get;
                set;
            }
            = MirrorPresetSlotSize;
            public string DataBase64 {
                get;
                set;
            }
            = "";
        }
        private class MirrorPresetSlotInfo {
            public int Index {
                get;
                set;
            }
            public bool IsEmpty {
                get;
                set;
            }
            public byte BodyType {
                get;
                set;
            }
            public uint FaceModel {
                get;
                set;
            }
            public uint HairModel {
                get;
                set;
            }
            public uint EyeModel {
                get;
                set;
            }
            public uint EyebrowModel {
                get;
                set;
            }
            public uint BeardModel {
                get;
                set;
            }
            public uint EyepatchModel {
                get;
                set;
            }
            public uint DecalModel {
                get;
                set;
            }
            public uint EyelashModel {
                get;
                set;
            }
            public byte[] RawData {
                get;
                set;
            }
            = Array.Empty<byte>();
            public string DisplayName => IsEmpty? "Slot " + (Index + 1) + " — Empty" : "Slot " + (Index + 1) + " — " + GetMirrorBodyTypeText(BodyType) + " • Face " + FaceModel + " • Hair " + HairModel;
            public override string ToString() {
                return DisplayName;
            }
        }
        private class CharacterStatsInfo {
            public uint Vigor {
                get;
                set;
            }
            public uint Mind {
                get;
                set;
            }
            public uint Endurance {
                get;
                set;
            }
            public uint Strength {
                get;
                set;
            }
            public uint Dexterity {
                get;
                set;
            }
            public uint Intelligence {
                get;
                set;
            }
            public uint Faith {
                get;
                set;
            }
            public uint Arcane {
                get;
                set;
            }
        }
        private class CharacterProfileInfo {
            public int SlotIndex {
                get;
                set;
            }
            public string Name {
                get;
                set;
            }
            = "";
            public uint Level {
                get;
                set;
            }
            public uint SecondsPlayed {
                get;
                set;
            }
            public uint Runes {
                get;
                set;
            }
            public byte BodyType {
                get;
                set;
            }
            public byte Archetype {
                get;
                set;
            }
        }
        private class CharacterSlotInfo {
            public int Index {
                get;
                set;
            }
            public string Name {
                get;
                set;
            }
            = "";
            public int Level {
                get;
                set;
            }
            public bool IsEmpty {
                get;
                set;
            }
            public bool IsActive {
                get;
                set;
            }
        }
        private class InstalledModPiece {
            public string ItemName {
                get;
                set;
            }
            = "";
            public string Type {
                get;
                set;
            }
            = "";
            public string ModelId {
                get;
                set;
            }
            = "";
            public string FileName {
                get;
                set;
            }
            = "";
            public string FullPath {
                get;
                set;
            }
            = "";
        }
        private class ParsedPartFile {
            public string Type {
                get;
                set;
            }
            = "";
            public string ModelId {
                get;
                set;
            }
            = "";
        }
        private class FileConflict {
            public DetectedModFile Source {
                get;
                set;
            }
            = new DetectedModFile();
            public DetectedModFile Existing {
                get;
                set;
            }
            = new DetectedModFile();
        }
        private class ConflictRecommendation {
            public string ModelId {
                get;
                set;
            }
            = "";
            public string Description {
                get;
                set;
            }
            = "";
        }
    }
}
