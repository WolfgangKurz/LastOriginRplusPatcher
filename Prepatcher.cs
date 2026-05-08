using System;

using BepInEx.Preloader.Core.Patching;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RplusPatcher
{
	[PatcherPluginInfo("com.swaytwig.lastorigin.rpluspatcher", "RplusPatcher", "1.0")]
	public class Prepatcher : BasePatcher {
		public override void Initialize() {
			Log.LogInfo("RplusPatcher initialized");
		}

		[TargetAssembly("Assembly-CSharp.dll")]
		public bool PatchAssembly(AssemblyDefinition assembly) {
			var module = assembly.MainModule;

			if (module.GetType("RplusSpriteSwitcher") != null) {
				Log.LogInfo("RplusSpriteSwitcher already exists. Skipping.");
				return false;
			}

			InjectClass(module);

			Log.LogInfo("Injected RplusSpriteSwitcher into Assembly-CSharp.dll");
			return true;
		}

		private static void InjectClass(ModuleDefinition module) {
			var monoBehaviourRef = ImportUnityType(module, "MonoBehaviour");
			var spriteRendererRef = ImportUnityType(module, "SpriteRenderer");
			var spriteRef = ImportUnityType(module, "Sprite");

			// public class RplusSpriteSwitcher : MonoBehaviour
			var typeRplusSpriteSwitcher = new TypeDefinition(
				"",
				"RplusSpriteSwitcher",
				TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
				monoBehaviourRef
			);
			module.Types.Add(typeRplusSpriteSwitcher);

			// public class SpriteCapture
			var typeSpriteCapture = new TypeDefinition(
				"",
				"SpriteCapture",
				TypeAttributes.NestedPublic | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
				module.TypeSystem.Object
			);
			typeRplusSpriteSwitcher.NestedTypes.Add(typeSpriteCapture);

			// SpriteCapture fields
			{
				// public SpriteRenderer _rederer;
				typeSpriteCapture.Fields.Add(new FieldDefinition("_rederer", FieldAttributes.Public, spriteRendererRef));

				// public Sprite _spriteOrigin;
				typeSpriteCapture.Fields.Add(new FieldDefinition("_spriteOrigin", FieldAttributes.Public, spriteRef));

				// public Sprite _spriteRplus;
				typeSpriteCapture.Fields.Add(new FieldDefinition("_spriteRplus", FieldAttributes.Public, spriteRef));
			}

			// typeof List<SpriteCapture>
			var typeSpriteCaptureList = BuildListGenericType(module, typeSpriteCapture);

			// RplusSpriteSwitcher fields
			// private List<SpriteCapture> _CapturedSprite;
			var field_CapturedSprite = new FieldDefinition("_CapturedSprite", FieldAttributes.Private, typeSpriteCaptureList);
			typeRplusSpriteSwitcher.Fields.Add(field_CapturedSprite);

			// SpriteCapture.ctor()
			{
				// public SpriteCapture() : base() {}
				var ctor = new MethodDefinition(
					".ctor",
					MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
					module.TypeSystem.Void
				);
				typeSpriteCapture.Methods.Add(ctor);

				var objectCtor = new MethodReference(".ctor", module.TypeSystem.Void, module.TypeSystem.Object) {
					HasThis = true
				};

				var il = ctor.Body.GetILProcessor();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, module.ImportReference(objectCtor));
				il.Emit(OpCodes.Ret);
			}

			// RplusSpriteSwitcher.ctor()
			{
				// public RplusSpriteSwitcher()
				var ctor = new MethodDefinition(
					".ctor",
					MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
					module.TypeSystem.Void
				);
				typeRplusSpriteSwitcher.Methods.Add(ctor);

				var monoBehaviourCtor = new MethodReference(".ctor", module.TypeSystem.Void, monoBehaviourRef) {
					HasThis = true
				};

				var listOfSpriteCapture = typeSpriteCaptureList;
				var listCtor = new MethodReference(".ctor", module.TypeSystem.Void, listOfSpriteCapture) {
					HasThis = true
				};

				var il = ctor.Body.GetILProcessor();

				// base..ctor()
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, module.ImportReference(monoBehaviourCtor));

				// this._CapturedSprite = new List<SpriteCapture>();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Newobj, module.ImportReference(listCtor));
				il.Emit(OpCodes.Stfld, field_CapturedSprite);

				il.Emit(OpCodes.Ret);
			}

			// RplusSpriteSwitcher.ActiveRplus
			{
				// public void ActivateRplus(bool rPlus)
				var method = new MethodDefinition(
					"ActivateRplus",
					MethodAttributes.Public | MethodAttributes.HideBySig,
					module.TypeSystem.Void
				);

				// (bool rPlus)
				method.Parameters.Add(new ParameterDefinition("rPlus", ParameterAttributes.None, module.TypeSystem.Boolean));
				method.Body.InitLocals = true;

				var intType = module.TypeSystem.Int32;
				var listOfSpriteCapture = typeSpriteCaptureList;

				// int i;
				var var_i = new VariableDefinition(intType);
				method.Body.Variables.Add(var_i);

				// SpriteCapture sp;
				var var_sp = new VariableDefinition(typeSpriteCapture);
				method.Body.Variables.Add(var_sp);

				typeRplusSpriteSwitcher.Methods.Add(method);

				var getCount = new MethodReference("get_Count", intType, listOfSpriteCapture) {
					HasThis = true
				};

				var listOpenType = listOfSpriteCapture.ElementType;
				var listGenericParameter = listOpenType.GenericParameters[0];
				var getItem = new MethodReference("get_Item", listGenericParameter, listOfSpriteCapture) {
					HasThis = true
				};
				getItem.Parameters.Add(new ParameterDefinition(intType)); // [i]

				// .sprite = R-Value
				var setSprite = new MethodReference("set_sprite", module.TypeSystem.Void, spriteRendererRef) {
					HasThis = true
				};
				setSprite.Parameters.Add(new ParameterDefinition(spriteRef)); // .sprite = sp

				var field_rederer = FindField(typeSpriteCapture, "_rederer");
				var field_spriteOrigin = FindField(typeSpriteCapture, "_spriteOrigin");
				var field_spriteRplus = FindField(typeSpriteCapture, "_spriteRplus");

				var il = method.Body.GetILProcessor();
				var originLoopStart = Instruction.Create(OpCodes.Nop);

				// if (!rPlus) goto originLoopStart;
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Brfalse, originLoopStart);

				// else loop: sp._rederer.sprite = sp._spriteRplus;
				{
					var loopCheck = Instruction.Create(OpCodes.Nop);
					var loopBody = Instruction.Create(OpCodes.Nop);

					// int i = 0;
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Stloc, var_i);
					il.Emit(OpCodes.Br, loopCheck);

					// loop body
					il.Append(loopBody);

					// sp = this._CapturedSprite[i];
					il.Emit(OpCodes.Ldarg_0); // this.
					il.Emit(OpCodes.Ldfld, field_CapturedSprite); // _CapturedSprite.
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Callvirt, getItem);
					il.Emit(OpCodes.Stloc, var_sp);

					// sp._rederer.sprite = sp._spriteRplus;
					il.Emit(OpCodes.Ldloc, var_sp);
					il.Emit(OpCodes.Ldfld, field_rederer);

					il.Emit(OpCodes.Ldloc, var_sp);
					il.Emit(OpCodes.Ldfld, field_spriteRplus);

					il.Emit(OpCodes.Callvirt, setSprite);

					// i++;
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Ldc_I4_1);
					il.Emit(OpCodes.Add);
					il.Emit(OpCodes.Stloc, var_i);

					// i < this._CapturedSprite.Count
					il.Append(loopCheck);
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Ldfld, field_CapturedSprite);
					il.Emit(OpCodes.Callvirt, getCount);
					il.Emit(OpCodes.Blt, loopBody);
				}

				il.Emit(OpCodes.Ret);

				// false loop: sp._rederer.sprite = sp._spriteOrigin;
				il.Append(originLoopStart);

				{
					var loopCheck = Instruction.Create(OpCodes.Nop);
					var loopBody = Instruction.Create(OpCodes.Nop);

					// int i = 0;
					il.Emit(OpCodes.Ldc_I4_0);
					il.Emit(OpCodes.Stloc, var_i);
					il.Emit(OpCodes.Br, loopCheck);

					// loop body
					il.Append(loopBody);

					// sp = this._CapturedSprite[i];
					il.Emit(OpCodes.Ldarg_0); // this.
					il.Emit(OpCodes.Ldfld, field_CapturedSprite); // _CapturedSprite.
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Callvirt, getItem);
					il.Emit(OpCodes.Stloc, var_sp);

					// sp._rederer.sprite = sp._spriteOrigin;
					il.Emit(OpCodes.Ldloc, var_sp);
					il.Emit(OpCodes.Ldfld, field_rederer);

					il.Emit(OpCodes.Ldloc, var_sp);
					il.Emit(OpCodes.Ldfld, field_spriteOrigin);

					il.Emit(OpCodes.Callvirt, setSprite);

					// i++;
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Ldc_I4_1);
					il.Emit(OpCodes.Add);
					il.Emit(OpCodes.Stloc, var_i);

					// i < this._CapturedSprite.Count
					il.Append(loopCheck);
					il.Emit(OpCodes.Ldloc, var_i);
					il.Emit(OpCodes.Ldarg_0);
					il.Emit(OpCodes.Ldfld, field_CapturedSprite);
					il.Emit(OpCodes.Callvirt, getCount);
					il.Emit(OpCodes.Blt, loopBody);
				}

				il.Emit(OpCodes.Ret);
			}

			// RplusSpriteSwitcher.Start()
			{
				var start = new MethodDefinition(
					"Start",
					MethodAttributes.Public | MethodAttributes.HideBySig,
					module.TypeSystem.Void
				);
				typeRplusSpriteSwitcher.Methods.Add(start);

				var activateRplus = FindMethod(typeRplusSpriteSwitcher, "ActivateRplus");
				var il = start.Body.GetILProcessor();

				// this.ActivateRplus(true);
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldc_I4_1);
				il.Emit(OpCodes.Call, activateRplus);
				il.Emit(OpCodes.Ret);
			}

			// CommonUtils.GetSpineCensorShipType
			CommonUtilsPatcher.PatchGetSpineCensorShipType(module);
		}

		private static GenericInstanceType BuildListGenericType(ModuleDefinition module, TypeReference itemType) {
			var listType = new TypeReference(
				"System.Collections.Generic",
				"List`1",
				module,
				module.TypeSystem.CoreLibrary
			);

			var t = new GenericParameter("T", listType);
			listType.GenericParameters.Add(t);

			var genericList = new GenericInstanceType(listType);
			genericList.GenericArguments.Add(itemType);

			return genericList;
		}

		private static TypeReference ImportUnityType(ModuleDefinition module, string typeName) {
			AssemblyNameReference unityScope = null;

			foreach (var assemblyRef in module.AssemblyReferences) {
				if (assemblyRef.Name == "UnityEngine.CoreModule" ||
					assemblyRef.Name == "UnityEngine") {
					unityScope = assemblyRef;
					break;
				}
			}

			if (unityScope == null)
				throw new InvalidOperationException("Could not find UnityEngine assembly reference.");

			var typeRef = new TypeReference("UnityEngine", typeName, module, unityScope);
			return module.ImportReference(typeRef);
		}

		private static FieldDefinition FindField(TypeDefinition type, string name) {
			foreach (var field in type.Fields) {
				if (field.Name == name)
					return field;
			}

			throw new InvalidOperationException("Field not found: " + name);
		}

		private static MethodDefinition FindMethod(TypeDefinition type, string name) {
			foreach (var method in type.Methods) {
				if (method.Name == name)
					return method;
			}

			throw new InvalidOperationException("Method not found: " + name);
		}
	}
}
