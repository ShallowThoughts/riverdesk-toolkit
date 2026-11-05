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
!define MUI_ICON "dist\RiverDeskToolkit.ico"
!define MUI_UNICON "dist\RiverDeskToolkit.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\RiverDeskToolkit.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\RiverDeskToolkit"

!macro CheckApplicationClosed PREFIX
Function ${PREFIX}CheckApplicationClosed
    retry:
    IfFileExists "$INSTDIR\RiverDeskToolkit.exe" 0 checkLibrary
    System::Call 'kernel32::CreateFileW(w "$INSTDIR\RiverDeskToolkit.exe", i 0x40000000, i 0, p 0, i 3, i 0, p 0) p.r0'
    IntCmp $0 -1 blocked
    System::Call 'kernel32::CloseHandle(p r0)'
    checkLibrary:
    IfFileExists "$INSTDIR\RiverDesk.Toolkit.dll" 0 done
    System::Call 'kernel32::CreateFileW(w "$INSTDIR\RiverDesk.Toolkit.dll", i 0x40000000, i 0, p 0, i 3, i 0, p 0) p.r0'
    IntCmp $0 -1 blocked
    System::Call 'kernel32::CloseHandle(p r0)'
    Goto done
    blocked:
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "暂时无法更新程序文件。请关闭 RiverDesk Toolkit 后重试。" IDRETRY retry
    Abort
    done:
FunctionEnd
!macroend
!insertmacro CheckApplicationClosed ""
!insertmacro CheckApplicationClosed "un."

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
    Call CheckApplicationClosed
    SetOutPath "$INSTDIR"
    File "dist\RiverDesk.Toolkit.dll"
    File "dist\RiverDeskToolkit.exe"
    File /oname=LICENSE.txt "LICENSE"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateDirectory "$SMPROGRAMS\RiverDesk Toolkit"
    CreateShortcut "$SMPROGRAMS\RiverDesk Toolkit\RiverDesk Toolkit.lnk" "$INSTDIR\RiverDeskToolkit.exe" "" "$INSTDIR\RiverDeskToolkit.exe" 0
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "RiverDesk Toolkit"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "RiverXILee"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" '"$INSTDIR\RiverDeskToolkit.exe",0'
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
    Call un.CheckApplicationClosed
    Delete "$INSTDIR\RiverDeskToolkit.exe"
    Delete "$INSTDIR\RiverDesk.Toolkit.dll"
    Delete "$INSTDIR\LICENSE.txt"
    Delete "$INSTDIR\Uninstall.exe"
    Delete "$INSTDIR\Updates\RiverDeskToolkit-*.exe"
    RMDir "$INSTDIR\Updates"
    RMDir "$INSTDIR"
    Delete "$SMPROGRAMS\RiverDesk Toolkit\RiverDesk Toolkit.lnk"
    RMDir "$SMPROGRAMS\RiverDesk Toolkit"
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
