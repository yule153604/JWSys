; -- JWSystem 安装程序脚本 --
; 由 Inno Setup 创建

[Setup]
AppName=JWSystem for CUPK
AppVersion=1.0
AppPublisher=JWSystem Developer
AppPublisherURL=https://github.com/your-username/jwsystem
AppSupportURL=https://github.com/your-username/jwsystem/issues
DefaultDirName={autopf}\JWSystem
DefaultGroupName=JWSystem
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=JWSystem-Setup-v1.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=
UninstallDisplayIcon={app}\JWSystem.exe
LicenseFile=
PrivilegesRequired=lowest
MinVersion=10.0.17763

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
; 主程序文件
Source: "publish\JWSystem.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\JWSystem.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\JWSystem.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\JWSystem.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

; 配置文件
Source: "publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\appsettings.Development.json"; DestDir: "{app}"; Flags: ignoreversion

; 依赖库
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; 脚本文件
Source: "publish\run.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\setup-env.ps1"; DestDir: "{app}"; Flags: ignoreversion

; 文档文件
Source: "publish\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\QUICK_START.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\JWSystem"; Filename: "{app}\JWSystem.exe"; Comment: "启动JWSystem教务系统"
Name: "{group}\快速开始指南"; Filename: "{app}\QUICK_START.md"; Comment: "查看快速开始指南"
Name: "{group}\卸载JWSystem"; Filename: "{uninstallexe}"
Name: "{autodesktop}\JWSystem"; Filename: "{app}\JWSystem.exe"; Comment: "启动JWSystem教务系统"; Tasks: desktopicon

[Run]
Filename: "{app}\JWSystem.exe"; Description: "启动JWSystem"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\previous_grades_data.json"
Type: dirifempty; Name: "{app}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 询问是否删除用户数据
    if MsgBox('是否删除用户数据文件（包括成绩记录）？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DeleteFile(ExpandConstant('{app}\previous_grades_data.json'));
    end;
  end;
end;
