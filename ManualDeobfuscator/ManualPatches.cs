using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ManualDeobfuscator
{
	public class ManualPatches : PatchHelpers
	{
		public static void applyManualPatches (ModuleDefinition mainModule)
		{
			OnElement ("StaticDirectories", mainModule.Types, type => {
				return Find ("StaticDirectories", type.Methods, method => {
					return !method.IsConstructor && method.IsPublic && method.IsStatic && method.Parameters.Count == 0 && HasType (method.ReturnType, "System.Boolean") && method.Name.Equals ("CheckIfStartedAsDedicatedServer");
				}
				) != null;
			}, RenameAction<TypeDefinition> ("StaticDirectories"));


			OnElement ("DamageSource", mainModule.GetType ("EntityPlayer").BaseType.Resolve ().Methods,
			    method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 3 && method.Name.Equals ("DamageEntity"),
				method => {
				RenameAction<TypeDefinition> ("DamageSource") (method.Parameters [0].ParameterType.Resolve ());
				return true;
			}
			);

			OnElement ("EntityPlayer.base.base.getPosition()", mainModule.GetType ("EntityPlayer").BaseType.Resolve ().BaseType.Resolve ().Methods,
			          method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 && method.Name.Equals ("GetPosition"),
			          method => {
				MethodBody body = method.Body;
				body.SimplifyMacros ();
				for (int i = 0; i < body.Instructions.Count; i++) {
					Instruction curInstr = body.Instructions [i];
					if (curInstr.OpCode == OpCodes.Ldfld) {
						if (curInstr.Operand is FieldDefinition) {
							RenameAction<FieldDefinition> ("position") ((FieldDefinition)curInstr.Operand);
						} else
							logger.Error ("A Ldfld instruction has no FieldDefinition");
					}
				}

				body.OptimizeMacros ();
				return true;
			}
			);

			// Rename method which generates map colors
			{
				MethodDefinition mapColors = Find ("Chunk.GetMapColors()", mainModule.GetType ("Chunk").Methods, method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
					method.Name.Equals ("GetMapColors")
				);
				if (mapColors != null) {
					MethodBody body = mapColors.Body;
					body.SimplifyMacros ();
					for (int i = 1; i < body.Instructions.Count; i++) {
						Instruction curInstr = body.Instructions [i];
						if (curInstr.OpCode == OpCodes.Call) {
							if (curInstr.Operand is MethodDefinition) {
								MethodDefinition md = (MethodDefinition)curInstr.Operand;
								RenameAction<MethodDefinition> ("CalcMapColors") (md);
								MakeMethodPublicAction (md);
							} else
								logger.Warning (" (Chunk.GetMapColors()): A Call instruction has no MethodDefinition operand!");
						}
					}
					body.OptimizeMacros ();
				}
			}

			OnElement ("ConnectionManager.DisconnectClient()", mainModule.GetType ("ConnectionManager").Methods,
			          method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 2 &&
				HasType (method.Parameters [0].ParameterType, "ClientInfo") && 
				HasType (method.Parameters [1].ParameterType, "System.Boolean") &&
				HasType (method.ReturnType, "System.Void"),
			          RenameAction<MethodDefinition> ("DisconnectClient"));

			RenameAction<TypeDefinition> ("ItemBase") (mainModule.GetType ("ItemClassBlock").BaseType.Resolve ());


			OnElement ("PersistentPlayerList.positionToLPBlockOwner", mainModule.GetType ("PersistentPlayerList").Fields,
			          field => HasType (field.FieldType, "System.Collections.Generic.Dictionary") && HasGenericParams (field.FieldType, "Vector3i", "PersistentPlayerData"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("positionToLPBlockOwner"));
				
			OnElement ("PlayerDataFile.inventory", mainModule.GetType ("PlayerDataFile").Fields,
			          field => field.Name.Equals ("inventory") && field.FieldType.IsArray,
			          field => {
				TypeDefinition invType = field.FieldType.Resolve ();
				RenameAction<TypeDefinition> ("InventoryField") (invType);
				OnElement ("InventoryField.count", invType.Fields,
				          field2 => HasType (field2.FieldType, "System.Int32"),
				          RenameAction<FieldDefinition> ("count"));
				OnElement ("InventoryField.itemValue", invType.Fields,
				          field2 => HasType (field2.FieldType, "ItemValue"),
				          RenameAction<FieldDefinition> ("itemValue"));
				return true;
			}
			);


			OnElement ("World.gameTime", mainModule.GetType ("World").Fields,
			          field => HasType (field.FieldType, "System.UInt64"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("gameTime"));


			OnElement ("World.LandClaimIsActive()", mainModule.GetType ("World").Methods,
			          method => !method.IsConstructor && method.IsPrivate && method.Parameters.Count == 1 && HasType (method.Parameters [0].ParameterType, "PersistentPlayerData") && HasType (method.ReturnType, "System.Boolean"),
			          MakeMethodPublicAction, RenameAction<MethodDefinition> ("LandClaimIsActive"));


			OnElement ("World.LandClaimPower()", mainModule.GetType ("World").Methods,
			          method => !method.IsConstructor && method.IsPrivate && method.Parameters.Count == 1 && HasType (method.Parameters [0].ParameterType, "PersistentPlayerData") && HasType (method.ReturnType, "System.Single"),
			          MakeMethodPublicAction, RenameAction<MethodDefinition> ("LandClaimPower"));


			OnElement ("ConnectionManager.connectedClients", mainModule.GetType ("ConnectionManager").Fields,
			          field => HasType (field.FieldType, "DictionarySave") && HasGenericParams (field.FieldType, "System.Int32", "ClientInfo"),
			          MakeFieldPublicAction, RenameAction<FieldDefinition> ("connectedClients"));


			// Console and ConsoleCommand
			{
				TypeDefinition typeConsole = null;
				TypeDefinition typeConsoleCommand = null;

				OnElement ("NetTelnetServer.SetConsole()", mainModule.GetType ("NetTelnetServer").Methods,
			          		method => !method.IsConstructor && method.Name.Equals ("SetConsole") && method.IsPublic && method.Parameters.Count == 1 && HasType (method.ReturnType, "System.Void"),
					           method => {
					typeConsole = method.Parameters [0].ParameterType.Resolve ();
					return true; }
				);

				// Console
				if (typeConsole != null) {
					RenameAction<TypeDefinition> ("ConsoleSdtd") (typeConsole);


					OnElement ("NetTelnetServer.console", mainModule.GetType ("NetTelnetServer").Fields,
			          		field => HasType (field.FieldType, typeConsole.Name),
			          		RenameAction<FieldDefinition> ("console"));

				
					OnElement ("ConsoleSdtd.telnetServer", typeConsole.Fields,
			          		field => HasType (field.FieldType, "NetTelnetServer"),
					        MakeFieldPublicAction, RenameAction<FieldDefinition> ("telnetServer"));


					OnElement ("ConsoleSdtd.ExecuteCmdFromClient()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 4 &&
						HasType (method.Parameters [0].ParameterType, "System.Int32") &&
						HasType (method.Parameters [1].ParameterType, "System.String") && 
						HasType (method.Parameters [2].ParameterType, "System.String") && 
						HasType (method.Parameters [3].ParameterType, "System.String") && 
						HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("ExecuteCmdFromClient")
					);


					OnElement ("ConsoleSdtd.Run()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.IsVirtual && method.Parameters.Count == 0 &&
						HasType (method.ReturnType, "System.Void") &&
						method.Body.CodeSize > 20,
						RenameAction<MethodDefinition> ("Run")
					);


					OnElement ("ConsoleSdtd.SendResult()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
						HasType (method.Parameters [0].ParameterType, "System.String") &&
						method.Parameters [0].Name.Equals ("_line") &&
						HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("SendResult")
					);


					OnElement ("ConsoleSdtd.ExecuteRemoteCmdInternal()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPrivate && method.Parameters.Count == 2 &&
						HasType (method.Parameters [0].ParameterType, "System.String") &&
						HasType (method.Parameters [1].ParameterType, "System.Boolean") &&
						HasType (method.ReturnType, "System.Void"),
					    MakeMethodPublicAction,
						RenameAction<MethodDefinition> ("ExecuteRemoteCmdInternal")
					);


					OnElement ("ConsoleSdtd.getCommand()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
						HasType (method.Parameters [0].ParameterType, "System.String") && method.Parameters [0].Name.Equals ("_command") &&
						method.ReturnType.Namespace.Length == 0,
					        method => {
						typeConsoleCommand = method.ReturnType.Resolve ();
						return true; },
						RenameAction<MethodDefinition> ("getCommand")
					);


					if (typeConsoleCommand != null) {
						OnElement ("ConsoleSdtd.AddCommand()", typeConsole.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
							HasType (method.Parameters [0].ParameterType, typeConsoleCommand.Name) &&
							HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("AddCommand")
						);


						OnElement ("ConsoleSdtd.commands", typeConsole.Fields,
				          		field => HasType (field.FieldType, "System.Collections.Generic.List") && HasGenericParams (field.FieldType, typeConsoleCommand.Name),
						        MakeFieldPublicAction, RenameAction<FieldDefinition> ("commands"));
					}

				}
				// END Console

				// Commands
				if (typeConsoleCommand != null) {
					RenameAction<TypeDefinition> ("ConsoleCommand") (typeConsoleCommand);
					PatchConsoleCommandMethods (typeConsoleCommand, typeConsole, "ConsoleCommand");

					// Individual command classes
					foreach (TypeDefinition td in mainModule.Types) {
						if (td.BaseType != null && td.BaseType.Name.Equals (typeConsoleCommand.Name)) {
							string commandName = string.Empty;
							MethodDefinition namesMethod = Find ("Command.Names()", td.Methods, method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
								HasType (method.ReturnType, "System.String[]")
							);
							if (namesMethod != null) {
								MethodBody body = namesMethod.Body;
								body.SimplifyMacros ();
								for (int i = 1; i < body.Instructions.Count; i++) {
									Instruction curInstr = body.Instructions [i];
									if (curInstr.OpCode == OpCodes.Ldstr) {
										if (curInstr.Operand is string) {
											string curName = (string)curInstr.Operand;
											if (curName.Length > commandName.Length) {
												commandName = curName;
											}
										} else
											logger.Warning ("A Ldstr instruction has no string operand!");
									}
								}
								if (commandName.Length > 0)
									RenameAction<TypeDefinition> ("Command_" + commandName) (td);
								else
									logger.Warning ("No name for command found");

								body.OptimizeMacros ();
							} else {
								logger.Warning ("No Names() method for command found");
							}
							PatchConsoleCommandMethods (td, typeConsole, commandName.Length > 0 ? "Command_" + commandName : "UnknownCommand");

							foreach (TypeDefinition td2 in mainModule.Types) {
								if (td2.BaseType != null && td2.BaseType.Name.Equals (td.Name)) {
									logger.Info ("Base for: " + clnamestomod [td] + " - " + td2.Name);
								}
							}

						}
					}
					// END Individual command classes
				}
				// END Commands

			}
			// END Console and ConsoleCommand

		}

		private static void PatchConsoleCommandMethods (TypeDefinition td, TypeDefinition consoleType, string typeName)
		{
			OnElement (typeName + ".Help()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
				HasType (method.Parameters [0].ParameterType, consoleType.Name) &&
				HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("Help")
			);

			OnElement (typeName + ".RepeatInterval()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.Int32"),
						RenameAction<MethodDefinition> ("RepeatInterval")
			);

			OnElement (typeName + ".Names()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.String[]"),
						RenameAction<MethodDefinition> ("Names")
			);

			OnElement (typeName + ".Run()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 1 &&
				HasType (method.Parameters [0].ParameterType, "System.String[]") &&
				HasType (method.ReturnType, "System.Void"),
						RenameAction<MethodDefinition> ("Run")
			);

			OnElement (typeName + ".Description()", td.Methods,
			          		method => !method.IsConstructor && method.IsPublic && method.Parameters.Count == 0 &&
				HasType (method.ReturnType, "System.String"),
						RenameAction<MethodDefinition> ("Description")
			);

		}

	}
}

