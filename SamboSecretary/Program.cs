namespace SamboSecretary;
static class Program{
    [STAThread]
    static void Main(){
        bool selfTest=Environment.GetCommandLineArgs().Any(a=>a.Equals("--selftest",StringComparison.OrdinalIgnoreCase));
        try{
            ApplicationConfiguration.Initialize();
            if(selfTest){
                using var form=new MainForm();
                form.CreateControl();
                Environment.ExitCode=0;
                return;
            }
            Application.Run(new MainForm());
        }catch(Exception ex){
            try{
                var d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SamboSecretary");
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d,"startup-error.log"),ex.ToString());
            }catch{}
            if(selfTest){Environment.ExitCode=1;return;}
            MessageBox.Show(ex.ToString(),"Ошибка запуска Самбо-секретарь",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}