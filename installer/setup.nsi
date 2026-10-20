; Built only inside the VPS Docker packaging stage.
Unicode true
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "WordFunc.nsh"
Name "${APPNAME} ${VERSION}"
OutFile "${SETUP_OUTPUT}"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ShowInstDetails show
Var InstalledProduct
Var InstalledVersion
Var Choice
Var Dialog
Var InstallRadio
Var RepairRadio
Var RemoveRadio
Var Comparison
!define MUI_ICON "${ICON_PATH}"
!define MUI_UNICON "${ICON_PATH}"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
Page custom ChooseAction LeaveAction
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_NOAUTOCLOSE
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "Russian"

Function .onInit
 StrCpy $InstalledProduct ""
 StrCpy $InstalledVersion ""
 StrCpy $Choice "install"
 ; Detect legacy MSI releases by the same UpgradeCode, not executable paths.
 System::Call 'msi::MsiEnumRelatedProductsW(w "${UPGRADE_CODE}", i 0, i 0, w .r1) i.r0'
 ${If} $0 == 0
  StrCpy $InstalledProduct $1
  StrCpy $3 256
  System::Call 'msi::MsiGetProductInfoW(w r1, w "VersionString", w .r2, *i r3) i.r0'
  ${If} $0 != 0
   MessageBox MB_ICONSTOP "Не удалось прочитать сведения об установленной версии. Исправьте регистрацию Windows Installer и повторите запуск."
   Abort
  ${EndIf}
  StrCpy $InstalledVersion $2
  ${VersionCompare} "$InstalledVersion" "${VERSION}" $Comparison
  ; Multiple registrations require explicit cleanup, never arbitrary uninstall.
  System::Call 'msi::MsiEnumRelatedProductsW(w "${UPGRADE_CODE}", i 0, i 1, w .r1) i.r0'
  ${If} $0 == 0
   MessageBox MB_ICONSTOP "Найдено несколько установленных версий ${APPNAME}. Удалите лишние записи через «Установленные приложения» Windows и повторите запуск. Настройки пользователей сохраняются."
   Abort
  ${EndIf}
 ${ElseIf} $0 != 259
  MessageBox MB_ICONSTOP "Ошибка определения установленной версии Windows Installer: $0."
  Abort
 ${EndIf}
FunctionEnd

Function ChooseAction
 !insertmacro MUI_HEADER_TEXT "Установка ${APPNAME}" "Выберите действие"
 nsDialogs::Create 1018
 Pop $Dialog
 ${If} $Dialog == error
  Abort
 ${EndIf}
 ${If} $InstalledProduct == ""
  ${NSD_CreateLabel} 0 0 100% 35u "Будет установлен ${APPNAME} ${VERSION}.$\r$\nПеред установкой закройте приложение."
  Pop $0
  ${NSD_CreateRadioButton} 0 45u 100% 20u "Установить ${VERSION}"
  Pop $InstallRadio
  ${NSD_Check} $InstallRadio
 ${Else}
  ${NSD_CreateLabel} 0 0 100% 35u "Найдена установленная версия: $InstalledVersion.$\r$\nВерсия этого установщика: ${VERSION}. Закройте приложение перед продолжением."
  Pop $0
  ${NSD_CreateRadioButton} 0 45u 100% 20u "Обновить до ${VERSION} с сохранением настроек"
  Pop $InstallRadio
  ${NSD_CreateRadioButton} 0 75u 100% 20u "Исправить установленную версию $InstalledVersion"
  Pop $RepairRadio
  ${NSD_CreateRadioButton} 0 105u 100% 20u "Удалить приложение (сохранить настройки)"
  Pop $RemoveRadio
  ${If} $Comparison == 2
   ${NSD_Check} $InstallRadio
  ${Else}
   EnableWindow $InstallRadio 0
   ${NSD_Check} $RepairRadio
   ${If} $Comparison == 1
    ${NSD_CreateLabel} 0 140u 100% 25u "Установлена более новая версия. Понижение версии запрещено."
    Pop $0
   ${EndIf}
  ${EndIf}
 ${EndIf}
 nsDialogs::Show
FunctionEnd

Function LeaveAction
 StrCpy $Choice "install"
 ${If} $InstalledProduct != ""
  ${NSD_GetState} $RepairRadio $0
  ${If} $0 == ${BST_CHECKED}
   StrCpy $Choice "repair"
  ${EndIf}
  ${NSD_GetState} $RemoveRadio $0
  ${If} $0 == ${BST_CHECKED}
   StrCpy $Choice "remove"
   MessageBox MB_YESNO|MB_ICONQUESTION "Удалить ${APPNAME}? Пользователи, ключи и настройки останутся на компьютере." IDYES confirmed
   Abort
   confirmed:
  ${EndIf}
 ${EndIf}
FunctionEnd

Section
 InitPluginsDir
 SetOutPath "$PLUGINSDIR"
 ${If} $Choice == "install"
  File /oname=application.msi "${MSI_PATH}"
  DetailPrint "Установка ${APPNAME} ${VERSION}…"
  ClearErrors
  ExecWait '"$SYSDIR\msiexec.exe" /i "$PLUGINSDIR\application.msi" /passive /norestart /L*v "$TEMP\${APPNAME}-setup.log"' $0
 ${ElseIf} $Choice == "repair"
  DetailPrint "Восстановление версии $InstalledVersion…"
  ClearErrors
  ExecWait '"$SYSDIR\msiexec.exe" /fa $InstalledProduct /passive /norestart /L*v "$TEMP\${APPNAME}-setup.log"' $0
 ${Else}
  DetailPrint "Удаление ${APPNAME}…"
  ClearErrors
  ExecWait '"$SYSDIR\msiexec.exe" /x $InstalledProduct /passive /norestart /L*v "$TEMP\${APPNAME}-setup.log"' $0
 ${EndIf}
 ${If} ${Errors}
  StrCpy $0 1603
 ${EndIf}
 ${If} $0 == 3010
  SetRebootFlag true
  SetErrorLevel 3010
 ${ElseIf} $0 != 0
  MessageBox MB_ICONSTOP "Операция не завершена. Код Windows Installer: $0.$\r$\nЖурнал: $TEMP\${APPNAME}-setup.log.$\r$\nДля восстановления старой версии Windows может запросить её исходный MSI."
  SetErrorLevel $0
  Abort
 ${EndIf}
SectionEnd
