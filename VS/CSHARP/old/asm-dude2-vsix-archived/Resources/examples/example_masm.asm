
include "inc\example.inc"

	jmp	FOO



RemoveFileFromProject PROTO :HWND, 	:BOOLEAN
RemoveFileFromProject Proc Uses EBX EDI hChild:HWND, AskToSaveFirst:BOOLEAN
	Invoke GetWindowLong,hChild,0
	MOV EBX,EAX
    ret
RemoveFileFromProject endP

#region MASM relative jumps @@ jumps
#######################################################
	@@:
	jmp @F
	jmp @B
	xor rax, rax
	@@:
#endregion

#region Masm has local labels in procedures
#######################################################
proc_name1 PROC # add id to label graph and make code folding
    xor rax, rax
	local_label1:
    ret
proc_name1 ENDP

proc_name2 PROC # add id to label graph and make code folding
    xor rax, rax
	local_label1:
    ret
proc_name2 ENDP
#endregion

vertex STRUCT 
    .x  resq 1
    .y  resq 1
    .z  resq 1
vertex ENDS

segment_name SEGMENT USE64
    ASSUME gs:NOTHING
    mov eax, dword ptr gs:[0]
segment_name ENDS

segment_name SEGMENT USE64 # make code folding
    db "This string contains the word jmp inside of it",0
    FOO2 EQU 0x00
    mylabel LABEL near

    call proc_name
    jmp dword ptr [eax]
    jmp FOO2
    jmp mylabel
proc_name PROC # add id to label graph and make code folding
    xor rax, rax
    ret
proc_name ENDP
segment_name ENDS

#region MASM Alias
#######################################################
	alias label_alias = FOO
	jmp label_alias
#endregion

#region MASM keywords folding
#######################################################
	.While BYTE PTR [EDI]!=0
		.If BYTE PTR [EDI]==" "
			MOV BYTE PTR [EDI],0
						
			;Check if not aready in the list
			Invoke SendMessage,hListVariables,LVM_FINDITEM,-1,ADDR lvfi
			.If EAX==-1 ;i.e if there is NOT such text in the list
				Invoke SendMessage,hListVariables,LVM_INSERTITEM,0,ADDR lvi
			.EndIf
			MOV BYTE PTR [EDI]," "
			INC EDI
						
			MOV lvi.pszText,EDI
			MOV lvfi.psz,EDI
		.EndIf
		INC EDI
	.EndW
	.If BYTE PTR [EDI]==":"
		MOV BYTE PTR [EDI],0
		INC EDI
		MOV EBX,EDI
		JMP @B
		;JMP NextOne
	.ElseIf BYTE PTR [EDI]==","
		MOV BYTE PTR [EDI],0
		MOV ESI,EDI
		INC ESI
	.Else
		.If BYTE PTR [EDI]!=0
			INC EDI
			JMP @B;NextOne
		.EndIf
	.EndIf
#endregion

ProjectPropertiesDlgProc_EndDialog Proc hDlg:DWORD
Local Buffer[32]:BYTE
Local LargeBuffer[MAX_PATH]:BYTE

	Invoke GetWindowText, hDlg, ADDR Buffer, 32
	Invoke lstrcmpi, ADDR Buffer, Offset szNewProjectDialogTitle
	.If EAX!=0 ;i.e This is NOT a New Project Dialog
		Invoke SendMessage, hNewProjectList, LVM_GETNEXTITEM, -1,  LVIS_SELECTED;LVNI_FOCUSED; +
		.If EAX!=ProjectType ;i.e user selected other type of project
			MOV ProjectType,EAX
			MOV ProjectModified,TRUE
		.Else
			MOV ECX,hTextRC
			CALL GetModify
			.If EAX	;i.e If User changed CompileRC arguments
				MOV ProjectModified,TRUE
			.Else
				MOV ECX,hReleaseTextML
				CALL GetModify
				.If EAX
					MOV ProjectModified,TRUE
				.Else
					MOV ECX,hReleaseTextLINK
					CALL GetModify

					call GetModify

					.If EAX
						MOV ProjectModified,TRUE
					.Else
						MOV ECX,hTextCVTRES
						CALL GetModify					
						.If EAX
							MOV ProjectModified,TRUE
						.Else
							MOV ECX,hReleaseTextOUT
							CALL GetModify					
							.If EAX
								MOV ProjectModified,TRUE
							.Else
								MOV ECX,hReleaseTextCommandLine
								CALL GetModify
								.If EAX
									MOV ProjectModified,TRUE
								.Else
									MOV ECX,hDebugTextML
									CALL GetModify
									.If EAX
										MOV ProjectModified,TRUE
									.Else
										MOV ECX,hDebugTextLINK
										CALL GetModify
										.If EAX
											MOV ProjectModified,TRUE
										.Else
											MOV ECX,hDebugTextOUT
											CALL GetModify					
											.If EAX
												MOV ProjectModified,TRUE
											.Else
												MOV ECX,hDebugTextCommandLine
												CALL GetModify
												.If EAX
													MOV ProjectModified,TRUE
												.EndIf
											.EndIf
										.EndIf
									.EndIf
								.EndIf
							.EndIf						
						.EndIf
					.EndIf
				.EndIf
			.EndIf
		.EndIf
		
		Invoke IsDlgButtonChecked,hDlg,32
		.If EAX!=bRCSilent
			MOV ProjectModified,TRUE
		.EndIf
		MOV bRCSilent,EAX
		
		Invoke IsDlgButtonChecked,hDlg,33
		.If EAX!=bPellesTools
			MOV ProjectModified,TRUE
		.EndIf
		MOV bPellesTools,EAX
		
		
		CALL GetMakeOptions
		Invoke SetProjectRelatedItems
	.Else	;New Project Dialog
		Invoke SendMessage,hTabControl,TCM_GETCURSEL,0,0
		.If EAX==0	;i.e. User selected one Empty Project (non template)
			Invoke ClearProject
			Invoke SendMessage, hNewProjectList, LVM_GETNEXTITEM, -1,LVIS_SELECTED; LVNI_FOCUSED; + 
			MOV ProjectType,EAX
			CALL GetMakeOptions
;+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
; by shoorick
;-----------------------------------------------------------------------
            invoke lstrcpy,Offset szProc,Offset dszProc
            invoke lstrcpy,Offset szEndp,Offset dszEndp
            invoke lstrcpy,Offset szMacro,Offset dszMacro
            invoke lstrcpy,Offset szEndm,Offset dszEndm
            invoke lstrcpy,Offset szStruct,Offset dszStruct
            invoke lstrcpy,Offset szEnds,Offset dszEnds
;-----------------------------------------------------------------------
            mov pIncludePath, offset IncludePath
            mov pKeyWordsFileName, offset KeyWordsFileName
            mov pAPIFunctionsFile, offset APIFunctionsFile
            mov pAPIStructuresFile, offset APIStructuresFile
            mov pAPIConstantsFile, offset APIConstantsFile
;-----------------------------------------------------------------------
            Invoke GetKeyWords
            Invoke GetAPIFunctions	
            Invoke GetAPIStructures
            Invoke GetAPIConstants
;+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
			Invoke DoNewProject,TRUE
		.Else
			MOV TabCtrlItem.imask,TCIF_TEXT
			LEA ECX,LargeBuffer
			MOV TabCtrlItem.pszText,ECX
			MOV TabCtrlItem.cchTextMax,SizeOf LargeBuffer-1
			Invoke SendMessage,hTabControl,TCM_GETITEM,EAX,ADDR TabCtrlItem
			
			Invoke RtlZeroMemory,Offset tmpBuffer2,SizeOf tmpBuffer2-1	;so that we have a second zero after the folder end when we copy it
			Invoke RtlZeroMemory,Offset tmpBuffer,SizeOf tmpBuffer-1	;so that we have a second zero after the folder end when we copy it
			
			Invoke lstrcpy,Offset tmpBuffer2, Offset szAppFilePath
			Invoke lstrcat,Offset tmpBuffer2, Offset szInTemplates	;"Templates\"
			Invoke lstrcat,Offset tmpBuffer2, ADDR LargeBuffer
			Invoke lstrcat,Offset tmpBuffer2, Offset szSlash
			Invoke SendMessage, hNewProjectList, LVM_GETNEXTITEM, -1, LVNI_FOCUSED; + LVIS_SELECTED;
			MOV ECX,EAX
			Invoke GetItemText, hNewProjectList,ECX,0,ADDR LargeBuffer
			Invoke lstrcat,Offset tmpBuffer2, ADDR LargeBuffer
			
			Invoke BrowseForAnyFolder,hDlg,0,Offset tmpBuffer
			.If EAX	;i.e. user did not select cancel
				Invoke SendMessage,hStatus,SB_SETTEXT,4,Offset szCreatingProject
				Invoke lstrcat,Offset tmpBuffer2,Offset szSlashAllFiles
				Invoke CopyAllFromTo,Offset tmpBuffer2,Offset tmpBuffer
				.If EAX==0	;If the same folder found and user pressed OK or Cancel then EAX<>0
					Invoke lstrlen,Offset tmpBuffer
					.If EAX!=3	;it is 3 if tmpBuffer=C:\, or D:\ etc. In such a case I will not add another slash
						Invoke lstrcat,Offset tmpBuffer, Offset szSlash
					.EndIf
					Invoke lstrcat,Offset tmpBuffer, ADDR LargeBuffer
					Invoke lstrcat,Offset tmpBuffer, Offset szExtwap
					Invoke OpenWAP,Offset tmpBuffer
				.EndIf
				Invoke SendMessage,hStatus,SB_SETTEXT,4,Offset szNULL
			.EndIf
		.EndIf
	.EndIf
	RET
	;------------------------------------------------------------------------	
	GetMakeOptions:
	
	
	MOV ECX,hTextRC
	LEA EDX,CompileRC
	CALL GetText

	MOV ECX,hReleaseTextML
	LEA EDX,szReleaseAssemble
	CALL GetText

	MOV ECX,hReleaseTextLINK
	LEA EDX,szReleaseLink
	CALL GetText


	MOV ECX,hDebugTextML
	LEA EDX,szDebugAssemble
	CALL GetText

	MOV ECX,hDebugTextLINK
	LEA EDX,szDebugLink
	CALL GetText

	;Trim any leading spaces
	MOV ECX,hTextCVTRES
	LEA EDX,LineTxt
	CALL GetText
	Invoke LTrim,Offset LineTxt,Offset RCToObj

	;Get ReleaseOutCommand and trim any leading spaces
	MOV ECX,hReleaseTextOUT
	LEA EDX,LineTxt
	CALL GetText
	Invoke LTrim,Offset LineTxt,Offset szReleaseOutCommand
	
	
	;Get DebugOutCommand and trim any leading spaces
	MOV ECX,hDebugTextOUT
	LEA EDX,LineTxt
	CALL GetText
	Invoke LTrim,Offset LineTxt,Offset szDebugOutCommand
	
	MOV ECX,hReleaseTextCommandLine
	LEA EDX,szReleaseCommandLine
	CALL GetText
	
	MOV ECX,hDebugTextCommandLine
	LEA EDX,szDebugCommandLine
	CALL GetText
	RETN
	
	;------------------------------------------------------------------------
	GetModify:
	Invoke SendMessage,ECX,EM_GETMODIFY,0,0
	RETN
	;------------------------------------------------------------------------
	GetText:
	Invoke SendMessage,ECX,WM_GETTEXT,256,EDX
	RETN
	;------------------------------------------------------------------------
ProjectPropertiesDlgProc_EndDialog EndP