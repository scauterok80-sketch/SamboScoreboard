namespace SamboSecretary;

public sealed record CategoryChoice(long Id,string Text){ public override string ToString()=>Text; }

public sealed partial class MainForm:Form{
    readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SamboSecretary");
    readonly Database db;
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};

    readonly TextBox tName=new(),tPlace=new(),tStart=new(),tEnd=new(),tChiefRef=new(),tChiefSec=new();
    readonly NumericUpDown tMats=new(){Minimum=1,Maximum=12,Value=1};

    readonly DataGridView categoryGrid=Grid();
    readonly DataGridView athleteGrid=Grid();
    readonly DataGridView weighGrid=Grid();
    readonly DataGridView judgeGrid=Grid();
    readonly DataGridView drawGrid=Grid();
    readonly DataGridView boutGrid=Grid();
    readonly DataGridView auditGrid=Grid();

    readonly ComboBox assignCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340};
    readonly ComboBox drawCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340};
    readonly ComboBox boutCategory=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=340};
    readonly ComboBox drawSystem=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=170};
    readonly ComboBox drawMode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=210};
    readonly ComboBox drawRepechage=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=220};

    public MainForm(){
        Text="Самбо-секретарь 2.0";WindowState=FormWindowState.Maximized;MinimumSize=new Size(1100,700);
        db=new Database(Path.Combine(root,"sambo.db"));
        BuildTournamentTab();BuildCategoriesTab();BuildAthletesTab();BuildWeighTab();BuildJudgesTab();BuildDrawTab();BuildBoutsTab();BuildResultsTab();BuildDocumentsTab();BuildAuditTab();
        Controls.Add(tabs);LoadTournament();ReloadAll();
    }

    static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};

    static Button Btn(string text,EventHandler click){
        var b=new Button{Text=text,AutoSize=true,Margin=new Padding(5)};b.Click+=click;return b;
    }

    TabPage Page(string title){var p=new TabPage(title);tabs.TabPages.Add(p);return p;}

    void ReloadAll(){
        ReloadCategories();ReloadAthletes();ReloadWeigh();ReloadJudges();ReloadCategoryChoices();ReloadDraw();ReloadBouts();ReloadResults();ReloadAudit();
    }

    void ReloadCategoryChoices(){
        var cats=db.Categories();
        CategoryChoice[] items=cats.Select(c=>new CategoryChoice(c.Id,$"{c.Discipline} | {c.Gender} | {c.AgeGroup} | {c.WeightCategory}")).ToArray();
        void Fill(ComboBox box){
            long? old=(box.SelectedItem as CategoryChoice)?.Id;box.Items.Clear();box.Items.AddRange(items);
            if(old.HasValue){for(int i=0;i<box.Items.Count;i++)if(((CategoryChoice)box.Items[i]).Id==old){box.SelectedIndex=i;return;}}
            if(box.Items.Count>0)box.SelectedIndex=0;
        }
        Fill(assignCategory);Fill(drawCategory);Fill(boutCategory);Fill(resultCategory);
    }

    long? SelectedCategory(ComboBox box)=>(box.SelectedItem as CategoryChoice)?.Id;
    long? SelectedId(DataGridView g){
        if(g.CurrentRow==null||g.CurrentRow.Cells.Count==0)return null;
        try{return Convert.ToInt64(g.CurrentRow.Cells[0].Value);}catch{return null;}
    }
}