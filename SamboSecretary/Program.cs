namespace SamboSecretary;
static class Program{
    [STAThread]
    static void Main(){
        try{
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }catch(Exception ex){
            try{
                var d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SamboSecretary");
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d,"startup-error.log"),ex.ToString());
            }catch{}
            MessageBox.Show(ex.ToString(),"Ошибка запуска Самбо-секретарь",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}