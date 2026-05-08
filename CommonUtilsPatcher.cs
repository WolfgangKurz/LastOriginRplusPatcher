using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RplusPatcher {
	internal class CommonUtilsPatcher {
		internal static void PatchGetSpineCensorShipType(ModuleDefinition module) {
			var commonUtils = module.GetType("CommonUtils");
			if (commonUtils == null)
				throw new InvalidOperationException("CommonUtils not found.");

			var method = commonUtils.Methods.FirstOrDefault(m => m.Name == "GetSpineCensorShipType" && m.IsStatic && m.Parameters.Count == 1);
			if (method == null)
				throw new InvalidOperationException("CommonUtils.GetSpineCensorShipType not found.");

			var typeExposedList_Skin = (GenericInstanceType)method.Parameters[0].ParameterType;
			var typeSkin = method.ReturnType;

			var findMethod = method.Body.Instructions
				.Select(i => i.Operand as MethodReference)
				.FirstOrDefault(m => m != null && m.Name == "Find" && m.DeclaringType.FullName.StartsWith("Spine.ExposedList`1"));
			if (findMethod == null)
				throw new InvalidOperationException("Original ExposedList<Skin>.Find reference not found.");

			var predicateCtor = method.Body.Instructions
				.Select(i => i.Operand as MethodReference)
				.FirstOrDefault(m => m != null && m.Name == ".ctor" && m.DeclaringType.FullName.StartsWith("System.Predicate`1"));
			if (predicateCtor == null)
				throw new InvalidOperationException("Original Predicate<Skin> constructor reference not found.");

			var predicateRPlus = BuildSkinNameMatcher(module, commonUtils, typeSkin, "__RplusPatcher_MatchRPlus", "breast/RPlus");
			var predicateUnedited = BuildSkinNameMatcher(module, commonUtils, typeSkin, "__RplusPatcher_MatchUnedited", "breast/Unedited");
			var predicateCensorship = BuildSkinNameMatcher(module, commonUtils, typeSkin, "__RplusPatcher_MatchCensorship", "breast/Censorship");

			// Replace method
			{
				method.Body.Variables.Clear();
				method.Body.Instructions.Clear();
				method.Body.ExceptionHandlers.Clear();
				method.Body.InitLocals = true;
				method.Body.MaxStackSize = 4;

				var resultLocal = new VariableDefinition(typeSkin);
				method.Body.Variables.Add(resultLocal);

				var il = method.Body.GetILProcessor();
				var returnResult = Instruction.Create(OpCodes.Ldloc, resultLocal);

				// result = skins.Find(predicateRPlus);
				{
					il.Emit(OpCodes.Ldarg_0); // skins

					// new Predicate<Skin>(null, ldftn matcher)
					il.Emit(OpCodes.Ldnull);
					il.Emit(OpCodes.Ldftn, predicateRPlus);
					il.Emit(OpCodes.Newobj, predicateCtor);

					// skins.Find(predicate)
					il.Emit(OpCodes.Callvirt, findMethod);
					il.Emit(OpCodes.Stloc, resultLocal);

					// if (result != null) return result;
					il.Emit(OpCodes.Ldloc, resultLocal);
					il.Emit(OpCodes.Brtrue, returnResult);
				}

				// result = skins.Find(predicateUnedited);
				{
					il.Emit(OpCodes.Ldarg_0); // skins

					// new Predicate<Skin>(null, ldftn matcher)
					il.Emit(OpCodes.Ldnull);
					il.Emit(OpCodes.Ldftn, predicateUnedited);
					il.Emit(OpCodes.Newobj, predicateCtor);

					// skins.Find(predicate)
					il.Emit(OpCodes.Callvirt, findMethod);
					il.Emit(OpCodes.Stloc, resultLocal);

					// if (result != null) return result;
					il.Emit(OpCodes.Ldloc, resultLocal);
					il.Emit(OpCodes.Brtrue, returnResult);
				}

				// result = skins.Find(predicateCensorship);
				{
					il.Emit(OpCodes.Ldarg_0); // skins

					// new Predicate<Skin>(null, ldftn matcher)
					il.Emit(OpCodes.Ldnull);
					il.Emit(OpCodes.Ldftn, predicateCensorship);
					il.Emit(OpCodes.Newobj, predicateCtor);

					// skins.Find(predicate)
					il.Emit(OpCodes.Callvirt, findMethod);
					il.Emit(OpCodes.Stloc, resultLocal);

					// return result;
					il.Append(returnResult);
					il.Emit(OpCodes.Ret);
				}
			}
		}

		// bool methodName(Skin x) => x.Name == expectedName
		private static MethodDefinition BuildSkinNameMatcher(ModuleDefinition module, TypeDefinition ownerType, TypeReference skinType, string methodName,
			string expectedName) {

			var existing = ownerType.Methods.FirstOrDefault(m => m.Name == methodName);
			if (existing != null)
				return existing;

			var matcher = new MethodDefinition(
				methodName,
				MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
				module.TypeSystem.Boolean
			);
			matcher.Parameters.Add(new ParameterDefinition("x", ParameterAttributes.None, skinType));
			// x =>

			ownerType.Methods.Add(matcher);

			var skinDef = skinType.Resolve();
			if (skinDef == null)
				throw new InvalidOperationException("Could not resolve Spine.Skin.");

			var getName = skinDef.Methods.FirstOrDefault(m => m.Name == "get_Name" && !m.HasParameters);
			if (getName == null)
				throw new InvalidOperationException("Spine.Skin.get_Name not found.");

			var stringEquals = module.TypeSystem.String.Resolve().Methods.FirstOrDefault(m =>
				m.Name == "Equals" &&
				m.IsStatic &&
				m.ReturnType.FullName == module.TypeSystem.Boolean.FullName &&
				m.Parameters.Count == 2 &&
				m.Parameters[0].ParameterType.FullName == module.TypeSystem.String.FullName &&
				m.Parameters[1].ParameterType.FullName == module.TypeSystem.String.FullName
			);
			if (stringEquals == null)
				throw new InvalidOperationException("System.String.Equals(string, string) not found.");

			// return x.Name == expectedName;
			var il = matcher.Body.GetILProcessor();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Callvirt, module.ImportReference(getName));
			il.Emit(OpCodes.Ldstr, expectedName);
			il.Emit(OpCodes.Call, module.ImportReference(stringEquals));
			il.Emit(OpCodes.Ret);
			return matcher;
		}

		// ExposedList<T>.Find
		private static MethodReference BuildExposedListFindMethod(ModuleDefinition module, GenericInstanceType exposedListOfSkin, TypeReference skinType) {
			var exposedListOpenType = exposedListOfSkin.ElementType;

			GenericParameter t;
			if (exposedListOpenType.GenericParameters.Count > 0) 
				t = exposedListOpenType.GenericParameters[0];
			else {
				t = new GenericParameter("T", exposedListOpenType);
				exposedListOpenType.GenericParameters.Add(t);
			}

			var predicateOfT = BuildPredicateOf(module, t);
			var find = new MethodReference("Find", t, exposedListOfSkin) {
				HasThis = true
			};
			find.Parameters.Add(new ParameterDefinition(predicateOfT));
			return find;
		}

		// typeof Predicate<T>
		private static GenericInstanceType BuildPredicateOf(ModuleDefinition module, TypeReference itemType) {
			var predicateType = new TypeReference("System", "Predicate`1", module, module.TypeSystem.CoreLibrary);

			var t = new GenericParameter("T", predicateType);
			predicateType.GenericParameters.Add(t);

			var predicateOfItem = new GenericInstanceType(predicateType);
			predicateOfItem.GenericArguments.Add(itemType);
			return predicateOfItem;
		}

		// Predicate<T>.ctor()
		private static MethodReference BuildPredicateCtor(ModuleDefinition module, GenericInstanceType predicateOfSkin) {
			var intPtrType = new TypeReference("System", "IntPtr", module, module.TypeSystem.CoreLibrary);
			var ctor = new MethodReference(".ctor", module.TypeSystem.Void, predicateOfSkin) {
				HasThis = true
			};

			ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
			ctor.Parameters.Add(new ParameterDefinition(intPtrType));
			return ctor;
		}
	}
}
