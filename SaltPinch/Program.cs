using System;
using System.Linq;
using System.Threading.Tasks;

using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.FormKeys.SkyrimLE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;

namespace SaltPinch {
   
public class Program {
   
public static async Task<int> Main(string[] args) {
   return await SynthesisPipeline.Instance
      .AddRunnabilityCheck(CheckRunnability)
      .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
      .SetTypicalOpen(GameRelease.SkyrimLE, "SaltPilePatcher.esp")
      .Run(args);
}

public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
   
   FormKey saltPinchKey = GetSaltFormKey(state.LoadOrder);
   
   //Function that returns true if an item is the Salt Pile
   Func<IContainerEntryGetter, bool> saltPileTest = 
      i => i.Item.Item.Equals(Skyrim.Ingredient.SaltPile);
   
   //Iterate through all recipes (ConstructibleObjects) in the load order
   foreach (var recipe in state.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides()) {
      
      //If the recipe does not have SaltPile as one of its ingredients, we skip it
      if (recipe.Items?.Any(saltPileTest) != true ||
      
      /*Try to resolve the created object of the recipe as a an Ingestible. If the product of
        the recipe is not ingestible, we skip.*/
      !recipe.CreatedObject.TryResolve<IIngestibleGetter>(state.LinkCache, out var productRecord) ||
      
      //See if the created object is food. If not, we skip.
      !productRecord.Flags.HasFlag(Ingestible.Flag.FoodItem)) continue;
      
      /*Now we are left only with recipes that take Salt Pile as one of the ingredients
        and produce an Ingestible flagged as food*/
      
      //Copy the recipe into the patch mod as an override
      (state.PatchMod.ConstructibleObjects.GetOrAddAsOverride(recipe)
      //Look in the items field (the ingredients for the recipe)
      .Items
      //Find the item that is a Salt Pile
      ?.FirstOrDefault(saltPileTest)?.Item?.Item
      /*Cast to an IFormLink instead of an IFormLinkGetter so it isn't read only
        IItemGetter includes all record types that are items (Ingredient, Book, MiscItem, etc.) */
      as IFormLink<IItemGetter>)
      //Set the record to salt pinch instead
      ?.SetTo(saltPinchKey);
   }
   
}

public static void CheckRunnability(IRunnabilityState state) {
   /*Need to get environment state so it can resolve Skyrim mods, otherwise types are
     different and it won't work. */
   GetSaltFormKey(state.GetEnvironmentState<ISkyrimMod, ISkyrimModGetter>().LoadOrder);
}

private static FormKey GetSaltFormKey(ILoadOrderGetter<IModListingGetter<ISkyrimModGetter>> loadOrder) {
   bool fileFound = false;
   string saltFileName = "SaltPinch.esp";
   string saltPinchEditorID = "SaltPinch";
   FormKey saltPinchKey = new FormKey();
   
   if (ModKey.TryFromFileName(saltFileName, out ModKey saltModKey)) {
      
      var saltModListing = loadOrder.TryGetValue(saltModKey);
      
      if (saltModListing?.Mod != null) {
         fileFound = true;
         
         /*convert the mod into a link cache, allows searching by EditorID.
           Uses an identifier only cache, since we don't need to retrieve the whole record.*/
         var saltLinkCache = saltModListing.Mod.
            ToImmutableLinkCache<ISkyrimMod, ISkyrimModGetter>(LinkCachePreferences.OnlyIdentifiers());
         
         //IItemGetter includes all record types that are items (Ingredient, Book, MiscItem, etc.)
         if (!saltLinkCache.TryResolveIdentifier<IItemGetter>(saltPinchEditorID, out saltPinchKey))
            throw new Exception(String.Format(
               "Item Record with EditorID \"{0}\" not found in \"{1}\"", saltPinchEditorID, saltFileName
            ));
      }
   }
   
   if (!fileFound) throw new Exception(String.Format(
      "File \"{0}\" not found. This mod is required for this patch.", saltFileName
   ));
   
   return saltPinchKey;
}

}

}
