#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System.Globalization;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using HarmonyLib;
global using RimWorld;
global using UnityEngine;
global using Verse;
#if USE_LABORATORY
global using ilyvion.Laboratory;
global using ilyvion.Laboratory.Coroutines;
global using Coroutine = System.Collections.Generic.IEnumerable<ilyvion.Laboratory.Coroutines.IResumeCondition>;
#endif
#pragma warning restore IDE0005 // Using directive is unnecessary.
