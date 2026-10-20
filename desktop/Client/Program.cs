namespace RemoteAdmin.Client;

internal static class Program {
 // WinForms drag-and-drop, Clipboard and OLE dialogs require an STA UI thread.
 [STAThread]
 private static void Main() {
  ApplicationConfiguration.Initialize();
  Application.Run(new MainWindow());
 }
}
