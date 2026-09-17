namespace KsEldenRingToolkitManager.Models;
public class EldenRingItem {
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
    public string Source {
        get;
        set;
    }
    = "";
    public bool IsDlc {
        get;
        set;
    }
    public string DlcText => IsDlc? "Shadow of the Erdtree" : "Base Game";
    public override string ToString() => Name;
}
