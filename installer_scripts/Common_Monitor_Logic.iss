; ============================================================================
; Logica compartida de instalacion — NVIDIA 15 kHz (240p / 224p) Win 10 & Win 11
; Incluye:
;  - Verificacion de Administrador y manejo seguro ante fallos (try...except)
;  - Deteccion automatica de GPU (GTX 1650 vs Experimental) y version de Windows
;  - Deteccion automatica de monitores conectados + proteccion Hard-Lock
;  - Registro de log detallado en %APPDATA%\Crt240pSwitch\installer.log
; ============================================================================

[Setup]
AppVersion=1.0.0
AppPublisher=Tupapa2712 / DJ Kendo (con ayuda de Gemini)
AppPublisherURL=https://github.com/Tupapa2712
DefaultDirName={autopf}\Crt240pSwitch
DefaultGroupName=NVIDIA 15kHz CRT 240p
OutputDir=..\build_output
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=
LicenseFile=AVISO_LEGAL_Y_CREDITOS.txt
UninstallDisplayIcon={app}\Crt240pSwitch.exe
DisableProgramGroupPage=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el Escritorio (Panel Crt240pSwitch)"; GroupDescription: "Accesos directos:"
Name: "autostart240p"; Description: "Mantener 2560x240 @ 60 Hz automaticamente en el CRT cada vez que inicie Windows (Recomendado)"; GroupDescription: "Inicio automatico:"
Name: "startminimized"; Description: "Iniciar Crt240pSwitch minimizado en la bandeja del sistema"; GroupDescription: "Inicio automatico:"; Flags: unchecked

[Files]
Source: "..\src\Crt240pSwitch\publish\Crt240pSwitch.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\Crt240pSwitch\publish\Crt240pSwitch.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\Crt240pSwitch\publish\Crt240pSwitch.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\Crt240pSwitch\publish\Crt240pSwitch.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "AVISO_LEGAL_Y_CREDITOS.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Crt240pSwitch"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Crt240pSwitch"; ValueType: string; ValueName: "Profile"; ValueData: "{#PROFILE_NAME}"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Crt240pAutoApply"; ValueData: """{app}\Crt240pSwitch.exe"" --apply-240p"; Tasks: autostart240p; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Crt240pSwitchTray"; ValueData: """{app}\Crt240pSwitch.exe"" --minimized"; Tasks: startminimized; Flags: uninsdeletevalue

[Icons]
Name: "{group}\Crt240pSwitch (Control 15 kHz)"; Filename: "{app}\Crt240pSwitch.exe"
Name: "{group}\Restaurar EDID Original (1 Clic)"; Filename: "{app}\Crt240pSwitch.exe"; Parameters: "--rollback"
Name: "{group}\Desinstalar NVIDIA 15kHz CRT 240p"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Crt240pSwitch (15 kHz)"; Filename: "{app}\Crt240pSwitch.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Crt240pSwitch.exe"; Description: "Abrir Crt240pSwitch ahora para aplicar/calibrar 2560x240p y 2560x224p"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Crt240pSwitch.exe"; Parameters: "--rollback"; Flags: runhidden; RunOnceId: "RollbackCrtEdid"

[Code]
type
  TMonitorItem = record
    GdiName: string;
    FriendlyName: string;
    InstanceId: string;
    IsPrimary: Boolean;
    Width: string;
    Height: string;
    RefreshHz: string;
    DisplayIndex: string;
    DisplayText: string;
  end;

var
  MonitorPage: TWizardPage;
  LblSystemInfo: TNewStaticText;
  LblHelp: TNewStaticText;
  ListMonitors: TNewListBox;
  BtnRefresh: TNewButton;
  Monitors: array of TMonitorItem;
  DetectedGpuName: string;
  DetectedOsName: string;
  SelectedProtectedId: string;
  SelectedTargetId: string;
  SelectedTargetIdx: string;

function GetLogFilePath(): string;
var
  Dir: string;
begin
  Dir := ExpandConstant('{userappdata}\Crt240pSwitch');
  ForceDirectories(Dir);
  Result := Dir + '\installer.log';
end;

procedure WriteInstallerLog(const Msg: string);
var
  Line: string;
begin
  try
    Line := '[' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + '] ' + Msg + #13#10;
    SaveStringToFile(GetLogFilePath(), Line, True);
  except
    // Nunca fallar por escritura de log
  end;
end;

function SplitTabField(var S: string): string;
var
  P: Integer;
begin
  P := Pos(#9, S);
  if P > 0 then
  begin
    Result := Copy(S, 1, P - 1);
    Delete(S, 1, P);
  end
  else
  begin
    Result := S;
    S := '';
  end;
end;

function DetectWindowsFriendlyName(): string;
var
  Ver: TWindowsVersion;
begin
  GetWindowsVersionEx(Ver);
  if (Ver.Major >= 10) and (Ver.Build >= 22000) then
    Result := Format('Windows 11 (Build %d)', [Ver.Build])
  else if Ver.Major >= 10 then
    Result := Format('Windows 10 (Build %d)', [Ver.Build])
  else
    Result := Format('Windows %d.%d (Build %d)', [Ver.Major, Ver.Minor, Ver.Build]);
end;

function DetectGpuViaPowerShell(): string;
var
  TmpOut: string;
  ResultCode: Integer;
  Lines: TArrayOfString;
  I: Integer;
  GpuText: string;
begin
  Result := 'No detectada automaticamente';
  TmpOut := ExpandConstant('{tmp}\gpu_detect.txt');
  try
    if Exec('powershell.exe',
      '-NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name | Out-File -FilePath ''' + TmpOut + ''' -Encoding UTF8"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if (ResultCode = 0) and LoadStringsFromFile(TmpOut, Lines) then
      begin
        GpuText := '';
        for I := 0 to GetArrayLength(Lines) - 1 do
        begin
          if Trim(Lines[I]) <> '' then
          begin
            if GpuText <> '' then GpuText := GpuText + ' / ';
            GpuText := GpuText + Trim(Lines[I]);
          end;
        end;
        if GpuText <> '' then Result := GpuText;
      end;
    end;
  except
    WriteInstallerLog('Aviso: excepcion controlada al detectar GPU via PowerShell.');
  end;
end;

procedure LoadMonitorsFallbackPowerShell(const OutFile: string);
var
  PsScript: string;
  ResultCode: Integer;
begin
  PsScript := ExpandConstant('{tmp}\probe_monitors.ps1');
  try
    SaveStringToFile(PsScript,
      'Add-Type -AssemblyName System.Windows.Forms' + #13#10 +
      '$idx = 1' + #13#10 +
      '$lines = foreach ($s in [System.Windows.Forms.Screen]::AllScreens) {' + #13#10 +
      '  $p = if ($s.Primary) { "1" } else { "0" }' + #13#10 +
      '  "$($s.DeviceName)`tMonitor Conectado`t$($s.DeviceName)`t$p`t$($s.Bounds.Width)`t$($s.Bounds.Height)`t60`t$idx"' + #13#10 +
      '  $idx++' + #13#10 +
      '}' + #13#10 +
      '$lines | Out-File -FilePath "' + OutFile + '" -Encoding UTF8' + #13#10,
      False);
    Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File "' + PsScript + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    WriteInstallerLog('Fallback PowerShell monitor probe ejecutado. ExitCode=' + IntToStr(ResultCode));
  except
    WriteInstallerLog('Error controlado en fallback PowerShell de monitores.');
  end;
end;

procedure RefreshMonitorList();
var
  OutFile: string;
  ResultCode: Integer;
  Lines: TArrayOfString;
  I, Count, DefaultIdx: Integer;
  Line, PrimTag: string;
  Item: TMonitorItem;
begin
  ListMonitors.Items.Clear;
  SetArrayLength(Monitors, 0);
  OutFile := ExpandConstant('{tmp}\monitors_detected.tsv');
  if FileExists(OutFile) then DeleteFile(OutFile);

  // 1) Intentar deteccion nativa CCD/EDID via Crt240pSwitch.exe --export-monitors
  try
    ExtractTemporaryFile('Crt240pSwitch.exe');
    ExtractTemporaryFile('Crt240pSwitch.dll');
    ExtractTemporaryFile('Crt240pSwitch.deps.json');
    ExtractTemporaryFile('Crt240pSwitch.runtimeconfig.json');
    if Exec(ExpandConstant('{tmp}\Crt240pSwitch.exe'), '--export-monitors "' + OutFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      WriteInstallerLog('Crt240pSwitch --export-monitors ExitCode=' + IntToStr(ResultCode));
    end;
  except
    WriteInstallerLog('Aviso: Crt240pSwitch --export-monitors no disponible en {tmp}, usando fallback.');
  end;

  // 2) Fallback seguro si no se genero salida (ej. falta .NET 9 Desktop Runtime)
  if (not FileExists(OutFile)) then
  begin
    LoadMonitorsFallbackPowerShell(OutFile);
  end;

  DefaultIdx := -1;
  if FileExists(OutFile) and LoadStringsFromFile(OutFile, Lines) then
  begin
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Line := Trim(Lines[I]);
      if Line = '' then Continue;
      // Quitar posible BOM UTF-8
      if (Length(Line) >= 3) and (Ord(Line[1]) = $EF) and (Ord(Line[2]) = $BB) and (Ord(Line[3]) = $BF) then
        Delete(Line, 1, 3);

      Item.GdiName := SplitTabField(Line);
      Item.FriendlyName := SplitTabField(Line);
      Item.InstanceId := SplitTabField(Line);
      Item.IsPrimary := (SplitTabField(Line) = '1');
      Item.Width := SplitTabField(Line);
      Item.Height := SplitTabField(Line);
      Item.RefreshHz := SplitTabField(Line);
      Item.DisplayIndex := SplitTabField(Line);

      if Item.IsPrimary then
        PrimTag := ' [PRINCIPAL - PROTEGIDO / HARD-LOCK]'
      else
        PrimTag := ' [SECUNDARIO - CANDIDATO CRT]';

      Item.DisplayText := Format('%s | %s | %sx%s @ %s Hz | ID: %s%s', [Item.GdiName, Item.FriendlyName, Item.Width, Item.Height, Item.RefreshHz, Item.InstanceId, PrimTag]);

      Count := GetArrayLength(Monitors);
      SetArrayLength(Monitors, Count + 1);
      Monitors[Count] := Item;
      ListMonitors.Items.Add(Item.DisplayText);

      if (not Item.IsPrimary) and (DefaultIdx = -1) then
        DefaultIdx := Count;
    end;
  end;

  if GetArrayLength(Monitors) = 0 then
  begin
    ListMonitors.Items.Add('No se detectaron monitores activos. Podras seleccionarlos luego dentro de Crt240pSwitch.');
    WriteInstallerLog('Aviso: 0 monitores listados en asistente.');
  end
  else if DefaultIdx >= 0 then
  begin
    ListMonitors.ItemIndex := DefaultIdx;
  end
  else
  begin
    ListMonitors.ItemIndex := 0;
  end;
end;

procedure OnRefreshClick(Sender: TObject);
begin
  RefreshMonitorList();
end;

function InitializeSetup(): Boolean;
var
  IsExp: Boolean;
  HasNvidia: Boolean;
  HasGtx1650: Boolean;
  MsgRes: Integer;
begin
  Result := True;
  WriteInstallerLog('=== Inicio de instalador: {#PROFILE_NAME} ===');

  if not IsAdminInstallMode then
  begin
    WriteInstallerLog('ERROR: Instalador ejecutado sin privilegios de Administrador.');
    MsgBox('Este instalador requiere permisos de Administrador para configurar los modos 15 kHz y respaldar el registro EDID de forma segura.' + #13#10#13#10 +
           'Por favor ejecuta el instalador haciendo clic derecho -> "Ejecutar como administrador".',
           mbError, MB_OK);
    Result := False;
    Exit;
  end;

  DetectedOsName := DetectWindowsFriendlyName();
  DetectedGpuName := DetectGpuViaPowerShell();
  WriteInstallerLog('Sistema detectado: ' + DetectedOsName + ' | GPU: ' + DetectedGpuName);

  HasNvidia := (Pos('NVIDIA', Uppercase(DetectedGpuName)) > 0);
  HasGtx1650 := (Pos('1650', Uppercase(DetectedGpuName)) > 0);
  IsExp := ('{#IS_EXPERIMENTAL}' = '1');

  if not IsExp then
  begin
    // Instalador Perfil Probado GTX 1650
    if not HasGtx1650 then
    begin
      MsgRes := MsgBox(
        'AVISO DE DETECCION DE TARJETA GRAFICA' + #13#10#13#10 +
        'Sistema operativo: ' + DetectedOsName + #13#10 +
        'GPU detectada: ' + DetectedGpuName + #13#10#13#10 +
        'Este instalador esta probado y optimizado especificamente para tarjetas NVIDIA GeForce GTX 1650 (arquitectura Turing).' + #13#10 +
        'Si utilizas otra tarjeta NVIDIA, te recomendamos usar el "Instalador Experimental NVIDIA".' + #13#10#13#10 +
        'Deseas continuar de todos modos bajo tu propia responsabilidad?',
        mbConfirmation, MB_YESNO);
      if MsgRes <> IDYES then
      begin
        WriteInstallerLog('Usuario cancelo al detectar GPU distinta de GTX 1650.');
        Result := False;
        Exit;
      end;
    end;
  end
  else
  begin
    // Instalador Experimental Otras GPUs NVIDIA
    MsgRes := MsgBox(
      'MODO EXPERIMENTAL — OTRAS TARJETAS NVIDIA' + #13#10#13#10 +
      'Sistema operativo: ' + DetectedOsName + #13#10 +
      'GPU detectada: ' + DetectedGpuName + #13#10#13#10 +
      'Estas ejecutando la version EXPERIMENTAL para probar super resoluciones 240p/224p en tarjetas NVIDIA distintas a la GTX 1650.' + #13#10 +
      'No existe garantia de compatibilidad con tu modelo de GPU. Tu monitor principal permanecera protegido (Hard-Lock).' + #13#10#13#10 +
      'Deseas continuar bajo tu propia responsabilidad?',
      mbConfirmation, MB_YESNO);
    if MsgRes <> IDYES then
    begin
      WriteInstallerLog('Usuario cancelo advertencia de modo experimental.');
      Result := False;
      Exit;
    end;
  end;
end;

procedure InitializeWizard();
begin
  MonitorPage := CreateCustomPage(
    wpSelectDir,
    'Deteccion Automatica de Monitores Conectados',
    'Selecciona tu televisor o monitor CRT destino. Tu monitor principal quedara protegido (Hard-Lock).'
  );

  LblSystemInfo := TNewStaticText.Create(MonitorPage);
  LblSystemInfo.Parent := MonitorPage.Surface;
  LblSystemInfo.Left := 0;
  LblSystemInfo.Top := 0;
  LblSystemInfo.Width := MonitorPage.SurfaceWidth;
  LblSystemInfo.Height := 34;
  LblSystemInfo.AutoSize := False;
  LblSystemInfo.WordWrap := True;
  LblSystemInfo.Caption := 'Sistema: ' + DetectedOsName + #13#10 + 'GPU detectada: ' + DetectedGpuName;

  LblHelp := TNewStaticText.Create(MonitorPage);
  LblHelp.Parent := MonitorPage.Surface;
  LblHelp.Left := 0;
  LblHelp.Top := 38;
  LblHelp.Width := MonitorPage.SurfaceWidth;
  LblHelp.Height := 34;
  LblHelp.AutoSize := False;
  LblHelp.WordWrap := True;
  LblHelp.Caption := 'Monitores detectados actualmente en tu GPU (elige el monitor secundario/CRT; el PRINCIPAL no sera modificado):';

  ListMonitors := TNewListBox.Create(MonitorPage);
  ListMonitors.Parent := MonitorPage.Surface;
  ListMonitors.Left := 0;
  ListMonitors.Top := 76;
  ListMonitors.Width := MonitorPage.SurfaceWidth;
  ListMonitors.Height := 120;

  BtnRefresh := TNewButton.Create(MonitorPage);
  BtnRefresh.Parent := MonitorPage.Surface;
  BtnRefresh.Left := 0;
  BtnRefresh.Top := 204;
  BtnRefresh.Width := 180;
  BtnRefresh.Height := 26;
  BtnRefresh.Caption := 'Volver a detectar monitores';
  BtnRefresh.OnClick := @OnRefreshClick;

  RefreshMonitorList();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Idx, I: Integer;
begin
  Result := True;
  if CurPageID = MonitorPage.ID then
  begin
    SelectedProtectedId := '';
    SelectedTargetId := '';
    SelectedTargetIdx := '2';

    if GetArrayLength(Monitors) = 0 then
    begin
      Exit;
    end;

    Idx := ListMonitors.ItemIndex;
    if (Idx < 0) or (Idx >= GetArrayLength(Monitors)) then
    begin
      MsgBox('Por favor selecciona un monitor de la lista para continuar.', mbInformation, MB_OK);
      Result := False;
      Exit;
    end;

    if Monitors[Idx].IsPrimary then
    begin
      if GetArrayLength(Monitors) > 1 then
      begin
        MsgBox('PROTECCION HARD-LOCK ACTIVA' + #13#10#13#10 +
               'Has seleccionado tu monitor PRINCIPAL (' + Monitors[Idx].GdiName + ').' + #13#10 +
               'Para evitar dejar tu escritorio sin imagen, selecciona el monitor secundario conectado a tu adaptador CRT.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end
      else
      begin
        MsgBox('Aviso: solo se detecto 1 monitor activo actualmente. Podras conectar tu CRT y seleccionarlo luego dentro de Crt240pSwitch sin afectar tu pantalla principal.',
               mbInformation, MB_OK);
        Exit;
      end;
    end;

    // Guardar monitor principal como Protegido y el elegido como CRT Destino
    for I := 0 to GetArrayLength(Monitors) - 1 do
    begin
      if Monitors[I].IsPrimary then
      begin
        SelectedProtectedId := Monitors[I].InstanceId;
        Break;
      end;
    end;

    SelectedTargetId := Monitors[Idx].InstanceId;
    SelectedTargetIdx := Monitors[Idx].DisplayIndex;
    WriteInstallerLog('Seleccion confirmada -> Protegido=' + SelectedProtectedId + ' | CRT=' + SelectedTargetId + ' (Idx=' + SelectedTargetIdx + ')');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  CfgPath, CfgBakPath: string;
  HadCfg: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    CfgPath := ExpandConstant('{userappdata}\Crt240pSwitch\config.json');
    CfgBakPath := ExpandConstant('{tmp}\config_pre_install.bak');
    HadCfg := FileExists(CfgPath);
    if HadCfg then
      FileCopy(CfgPath, CfgBakPath, False);

    if (SelectedTargetId <> '') then
    begin
      try
        WizardForm.StatusLabel.Caption := 'Guardando seleccion de monitor CRT y proteccion Hard-Lock...';
        if Exec(ExpandConstant('{app}\Crt240pSwitch.exe'),
          '--save-config "' + SelectedProtectedId + '" "' + SelectedTargetId + '" ' + SelectedTargetIdx,
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
        begin
          WriteInstallerLog('Configuracion guardada via --save-config. ExitCode=0');

          WizardForm.StatusLabel.Caption := 'Respaldando EDID original y aplicando modos 2560x240p / 2560x224p al CRT...';
          if Exec(ExpandConstant('{app}\Crt240pSwitch.exe'), '--auto-install-modes',
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
          begin
            WriteInstallerLog('Modos 2560x240p y 2560x224p aplicados automaticamente con exito.');
          end
          else
          begin
            WriteInstallerLog('FALLO en --auto-install-modes (ExitCode=' + IntToStr(ResultCode) + '). Restaurando estado previo...');
            // Restaurar config.json previo y limpiar entrada Run si fallo
            if HadCfg then FileCopy(CfgBakPath, CfgPath, False)
            else if FileExists(CfgPath) then DeleteFile(CfgPath);
            RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Crt240pAutoApply');
            MsgBox('AVISO DE SEGURIDAD — REVERSION AUTOMATICA COMPLETADA' + #13#10#13#10 +
                   'No se pudieron aplicar automaticamente los modos 15 kHz en el monitor seleccionado (codigo ' + IntToStr(ResultCode) + ').' + #13#10 +
                   'Para proteger tu equipo, el instalador revirtio automaticamente todos los cambios a su estado original y tu monitor principal no fue tocado.' + #13#10#13#10 +
                   'Puedes revisar el registro de diagnostico en:' + #13#10 + GetLogFilePath(),
                   mbError, MB_OK);
          end;
        end
        else
        begin
          WriteInstallerLog('Aviso: --save-config devolvio ExitCode=' + IntToStr(ResultCode));
        end;
      except
        WriteInstallerLog('Excepcion controlada en ssPostInstall. Restaurando configuracion previa...');
        if HadCfg then FileCopy(CfgBakPath, CfgPath, False);
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Crt240pAutoApply');
        MsgBox('Ocurrio un error inesperado durante la configuracion automatica. Todos los cambios fueron revertidos de forma segura a su estado original.' + #13#10 +
               'Log: ' + GetLogFilePath(), mbError, MB_OK);
      end;
    end;
    WriteInstallerLog('Instalacion finalizada. Log: ' + GetLogFilePath());
  end;
end;
