' Opens a separate copy of Claude Desktop for a second account, with no terminal window.
' To use another name, change the line below. Use the same name in the .bat file.
Const CONTA = "Trabalho"

Dim sh, pasta, comando, codigo
Set sh = CreateObject("WScript.Shell")
pasta = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
comando = "powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & pasta & "\abrir-conta.ps1"" -Nome """ & CONTA & """"
If WScript.Arguments.Count > 0 Then comando = comando & " -SoConferir"
codigo = sh.Run(comando, 0, True)

If codigo <> 0 Then
  MsgBox "Could not open Claude for this account." & vbCrLf & vbCrLf & _
         "Run the .bat file in this folder to see the error.", 48, "Claude"
End If
WScript.Quit codigo
