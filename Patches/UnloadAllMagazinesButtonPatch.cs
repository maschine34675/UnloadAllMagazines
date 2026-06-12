using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnloadAllMagazines.Patches
{
    internal class UnloadAllMagazinesButtonPatch : ModulePatch
    {
        private const string ButtonName = "UnloadAllMagazinesButton";

        private static readonly FieldInfo _fieldButton =
            AccessTools.Field(typeof(GridSortPanel), "_button");
        private static readonly FieldInfo _fieldController =
            AccessTools.Field(typeof(GridSortPanel), "inventoryController_0");
        private static readonly FieldInfo _fieldStash =
            AccessTools.Field(typeof(GridSortPanel), "compoundItem_0");

        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(GridSortPanel), "Show");

        [PatchPostfix]
        static void Postfix(GridSortPanel __instance)
        {
            var sortButton = (Button)_fieldButton.GetValue(__instance);
            var parent = sortButton.transform.parent;

            var existing = parent.Find(ButtonName);
            Button unloadButton;

            if (existing == null)
            {
                var go = Object.Instantiate(sortButton.gameObject, parent);
                go.name = ButtonName;
                go.transform.SetSiblingIndex(sortButton.transform.GetSiblingIndex());

                var sprite = CacheResourcesPopAbstractClass.Pop<Sprite>("Characteristics/Icons/UnloadAmmo");
                var label = go.GetComponentInChildren<TextMeshProUGUI>();

                if (sprite != null)
                {
                    foreach (var img in go.GetComponentsInChildren<Image>())
                    {
                        if (img.gameObject != go && img.sprite != null)
                        {
                            img.sprite = sprite;
                            img.preserveAspect = true;
                            break;
                        }
                    }
                    if (label != null)
                        label.text = string.Empty;
                }
                else
                {
                    if (label != null)
                        label.text = "MAG";
                }

                go.AddComponent<ButtonTooltip>().Text = "Unload all magazines";

                unloadButton = go.GetComponent<Button>();
            }
            else
            {
                unloadButton = existing.GetComponent<Button>();
            }

            unloadButton.onClick.RemoveAllListeners();
            var panel = __instance;
            unloadButton.onClick.AddListener(() => OnClick(panel, unloadButton));
        }

        static void OnClick(GridSortPanel panel, Button button)
        {
            var controller = _fieldController.GetValue(panel) as InventoryController;
            var stash      = _fieldStash.GetValue(panel) as CompoundItem;

            if (controller == null || stash == null)
            {
                Plugin.Log.LogWarning("[UnloadAllMagazines] Inventory not opened.");
                return;
            }

            button.interactable = false;
            UnloadAllMagazinesAsync(controller, stash)
                .ContinueWith(t =>
                {
                    button.interactable = true;
                    if (t.IsFaulted)
                        Plugin.Log.LogError($"[UnloadAllMagazines] Error: {t.Exception}");
                });
        }

        static async Task UnloadAllMagazinesAsync(InventoryController controller, CompoundItem stash)
        {
            var magazines = new List<MagazineItemClass>();
            CollectMagazines(stash, magazines);

            int count = 0;
            foreach (var mag in magazines)
            {
                if (mag.Count <= 0) continue;
                var result = await controller.UnloadMagazine(mag, false);
                if (!result.Failed)
                    count++;
                else
                    Plugin.Log.LogWarning($"[UnloadAllMagazines] {mag.Template._name}: {result.Error}");
            }

            NotificationManagerClass.DisplayMessageNotification(
                $"{count} Magazine(s) unloaded.",
                ENotificationDurationType.Default);
        }

        static void CollectMagazines(CompoundItem item, List<MagazineItemClass> result)
        {
            foreach (var grid in item.Grids ?? new StashGridClass[0])
                foreach (var gridItem in grid.Items)
                {
                    if (gridItem is MagazineItemClass mag) result.Add(mag);
                    else if (gridItem is CompoundItem sub) CollectMagazines(sub, result);
                }

            foreach (var slot in item.Slots ?? new Slot[0])
            {
                var slotItem = slot.ContainedItem;
                if (slotItem == null) continue;
                if (slotItem is MagazineItemClass mag) result.Add(mag);
                else if (slotItem is CompoundItem sub) CollectMagazines(sub, result);
            }
        }

        private class ButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IEventSystemHandler
        {
            public string Text;

            public void OnPointerEnter(PointerEventData _)
                => ItemUiContext.Instance.Tooltip.Show(Text, null, 0f, null);

            public void OnPointerExit(PointerEventData _)
                => ItemUiContext.Instance.Tooltip.Close();
        }
    }
}
