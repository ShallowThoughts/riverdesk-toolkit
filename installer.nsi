Unicode true
!include "MUI2.nsh"
!include "x64.nsh"

Name "RiverDesk Toolkit"
OutFile "dist\RiverDeskToolkit-Setup.exe"
InstallDir "$PROGRAMFILES\RiverDesk Toolkit"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "RiverDesk Toolkit"
VIAddVersionKey /LANG=2052 "FileDescription" "RiverDesk Toolkit Setup"
VIAddVersionKey /LANG=2052 "CompanyName" "RiverXILee"
VIAddVersionKey /LANG=2052 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "RiverXILee"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\RiverDeskToolkit.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\RiverDeskToolkit"

Function .onInit
    ${If} ${RunningX64}
        SetRegView 64
        StrCpy $INSTDIR "$PROGRAMFILES64\RiverDesk Toolkit"
    ${Else}
        StrCpy $INSTDIR "$PROGRAMFILES\RiverDesk Toolkit"
    ${EndIf}
    SetShellVarContext all
FunctionEnd

Section
    SetOutPath "$INSTDIR"
    File "dist\RiverDeskToolkit.exe"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateDirectory "$SMPROGRAMS\RiverDesk Toolkit"
    CreateShortcut "$SMPROGRAMS\RiverDesk Toolkit\RiverDesk Toolkit.lnk" "$INSTDIR\RiverDeskToolkit.exe"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "RiverDesk Toolkit"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "RiverXILee"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\RiverDeskToolkit.exe"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Function un.onInit
    ${If} ${RunningX64}
        SetRegView 64
    ${EndIf}
    SetShellVarContext all
FunctionEnd

Section "Uninstall"
    Delete "$INSTDIR\RiverDeskToolkit.exe"
    Delete "$INSTDIR\Uninstall.exe"
    Delete "$INSTDIR\Updates\RiverDeskToolkit-*.exe"
    RMDir "$INSTDIR\Updates"
    RMDir "$INSTDIR"
    Delete "$SMPROGRAMS\RiverDesk Toolkit\RiverDesk Toolkit.lnk"
    RMDir "$SMPROGRAMS\RiverDesk Toolkit"
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
