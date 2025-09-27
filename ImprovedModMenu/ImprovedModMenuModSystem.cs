using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

#nullable enable

namespace ImprovedModMenu
{
    public class ImprovedModMenuModSystem : ModSystem
    {
        public override void StartClientSide(ICoreClientAPI api)
        {
            HarmonyBootstrap.EnsurePatched();
        }

        public override void Dispose()
        {
            HarmonyBootstrap.Unpatch();
        }

        [HarmonyPatch(typeof(GuiScreenMods))]
        internal static class GuiScreenModsPatches
        {
            private enum SortMode
            {
                Name,
                Active
            }

            private sealed class SortState
            {
                public SortMode Mode = SortMode.Name;
            }

            private static readonly ConditionalWeakTable<GuiScreenMods, SortState> SortStates = new();
            private static readonly AccessTools.FieldRef<GuiScreenMods, ElementBounds> ListBoundsRef = AccessTools.FieldRefAccess<GuiScreenMods, ElementBounds>("listBounds");
            private static readonly AccessTools.FieldRef<GuiScreenMods, ElementBounds> ClippingBoundsRef = AccessTools.FieldRefAccess<GuiScreenMods, ElementBounds>("clippingBounds");
            private static readonly AccessTools.FieldRef<GuiScreenMods, ScreenManager> ScreenManagerRef = AccessTools.FieldRefAccess<GuiScreenMods, ScreenManager>("screenManager");

            private static readonly MethodInfo DialogBaseMethod = AccessTools.Method(typeof(GuiScreen), "dialogBase", new[] { typeof(string), typeof(double), typeof(double) });
            private static readonly MethodInfo LoadModCellsMethod = AccessTools.Method(typeof(GuiScreenMods), "LoadModCells");
            private static readonly MethodInfo CreateCellElemMethod = AccessTools.Method(typeof(GuiScreenMods), "createCellElem");
            private static readonly MethodInfo OnNewScrollbarValueMethod = AccessTools.Method(typeof(GuiScreenMods), "OnNewScrollbarvalue");
            private static readonly MethodInfo OnReloadModsMethod = AccessTools.Method(typeof(GuiScreenMods), "OnReloadMods");
            private static readonly MethodInfo OnOpenModsFolderMethod = AccessTools.Method(typeof(GuiScreenMods), "OnOpenModsFolder");

            private const double SortButtonWidth = 110.0;
            private const double SortButtonHeight = 30.0;
            private const double SortButtonHorizontalSpacing = 6.0;
            private const double SortButtonVerticalSpacing = 4.0;
            private const double TitleToButtonsSpacing = 8.0;
            private const double ButtonsToListSpacing = 5.0;

            [HarmonyPrefix]
            [HarmonyPatch("InitGui")]
            public static bool InitGuiPrefix(GuiScreenMods __instance)
            {
                SortStates.GetValue(__instance, _ => new SortState());

                __instance.ElementComposer?.Dispose();

                ScreenManager screenManager = ScreenManagerRef(__instance);
                int height = screenManager.GamePlatform.WindowSize.Height;
                int width = screenManager.GamePlatform.WindowSize.Width;

                ElementBounds baseButtonBounds = ElementBounds.FixedSize(60.0, 30.0)
                    .WithFixedPadding(10.0, 2.0)
                    .WithAlignment(EnumDialogArea.RightFixed);
                ElementBounds titleAnchorBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0.0, 0.0, 690.0, 35.0);
                ElementBounds titleBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0.0, 0.0, 690.0, 40.0);

                double scrollWidth = Math.Max(400.0, (double)width * 0.5) / ClientSettings.GUIScale;
                float scaledHeight = (float)Math.Max(300, height) / ClientSettings.GUIScale;
                bool canFitSideBySide = scrollWidth >= SortButtonWidth * 2.0 + SortButtonHorizontalSpacing;
                double buttonsHeight = canFitSideBySide
                    ? SortButtonHeight
                    : SortButtonHeight * 2.0 + SortButtonVerticalSpacing;
                double additionalOffset = TitleToButtonsSpacing + buttonsHeight + ButtonsToListSpacing;
                double scrollHeight = scaledHeight - 190f - additionalOffset;
                if (scrollHeight < 0.0)
                {
                    scrollHeight = 0.0;
                }

                ElementBounds scrollBounds = titleAnchorBounds
                    .BelowCopy(0.0, additionalOffset)
                    .WithFixedSize(scrollWidth, scrollHeight);

                GuiComposer composer = DialogBaseMethod.Invoke(__instance, new object[] { "mainmenu-mods", -1.0, -1.0 }) as GuiComposer
                    ?? throw new InvalidOperationException("dialogBase returned null");
                __instance.ElementComposer = composer;

                composer.AddStaticText(Lang.Get("Installed mods"), CairoFont.WhiteSmallishText(), titleBounds);
                composer.AddInset(scrollBounds);

                double buttonTop = titleAnchorBounds.fixedY + titleAnchorBounds.fixedHeight + TitleToButtonsSpacing;
                double scrollLeft = scrollBounds.fixedX;
                double scrollRight = scrollBounds.fixedX + scrollBounds.fixedWidth;

                ElementBounds sortNameBounds;
                ElementBounds sortActiveBounds;
                if (canFitSideBySide)
                {
                    double requiredWidth = SortButtonWidth * 2.0 + SortButtonHorizontalSpacing;
                    double startX = Math.Max(scrollLeft, scrollRight - requiredWidth);
                    sortNameBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, startX, buttonTop, SortButtonWidth, SortButtonHeight)
                        .WithFixedPadding(10.0, 2.0);
                    sortActiveBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, startX + SortButtonWidth + SortButtonHorizontalSpacing, buttonTop, SortButtonWidth, SortButtonHeight)
                        .WithFixedPadding(10.0, 2.0);
                }
                else
                {
                    sortNameBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, scrollLeft, buttonTop, SortButtonWidth, SortButtonHeight)
                        .WithFixedPadding(10.0, 2.0);
                    sortActiveBounds = ElementBounds.Fixed(EnumDialogArea.LeftTop, scrollLeft, buttonTop + SortButtonHeight + SortButtonVerticalSpacing, SortButtonWidth, SortButtonHeight)
                        .WithFixedPadding(10.0, 2.0);
                }

                composer.AddSmallButton(Lang.Get("improvedmodmenu-sort-name"), () => SetSortMode(__instance, SortMode.Name), sortNameBounds);
                composer.AddSmallButton(Lang.Get("improvedmodmenu-sort-active"), () => SetSortMode(__instance, SortMode.Active), sortActiveBounds);

                ElementBounds clippingBounds = scrollBounds.ForkContainingChild(3.0, 3.0, 3.0, 3.0);
                ElementBounds listBounds = clippingBounds.ForkContainingChild(0.0, 0.0, 0.0, -3.0).WithFixedPadding(5.0);
                ClippingBoundsRef(__instance) = clippingBounds;
                ListBoundsRef(__instance) = listBounds;

                Action<float> onScroll = value => OnNewScrollbarValueMethod.Invoke(__instance, new object[] { value });
                composer.AddVerticalScrollbar(onScroll, ElementStdBounds.VerticalScrollbar(scrollBounds), "scrollbar");

                OnRequireCell<ModCellEntry> cellCreator = (cell, bounds) =>
                {
                    object? created = CreateCellElemMethod.Invoke(__instance, new object[] { cell, bounds });
                    return created as IGuiElementCell
                        ?? throw new InvalidOperationException("createCellElem returned an unexpected value");
                };
                List<ModCellEntry> cells = InvokeLoadModCells(__instance);

                composer.BeginClip(clippingBounds);
                composer.AddCellList(listBounds, cellCreator, cells, "modstable");
                composer.EndClip();

                composer.AddSmallButton(Lang.Get("Reload Mods"), () => InvokeButtonHandler(__instance, OnReloadModsMethod),
                    baseButtonBounds.FlatCopy().FixedUnder(scrollBounds, 10.0).WithFixedAlignmentOffset(-13.0, 0.0));
                composer.AddSmallButton(Lang.Get("Open Mods Folder"), () => InvokeButtonHandler(__instance, OnOpenModsFolderMethod),
                    baseButtonBounds.FlatCopy().FixedUnder(scrollBounds, 10.0).WithFixedAlignmentOffset(-150.0, 0.0));

                composer.EndChildElements();
                composer.Compose();

                listBounds.CalcWorldBounds();
                clippingBounds.CalcWorldBounds();
                GuiElementScrollbar scrollbar = composer.GetScrollbar("scrollbar");
                if (scrollbar != null)
                {
                    scrollbar.SetHeights((float)clippingBounds.fixedHeight, (float)listBounds.fixedHeight);
                }

                return false;
            }

            [HarmonyPostfix]
            [HarmonyPatch("LoadModCells")]
            public static void LoadModCellsPostfix(GuiScreenMods __instance, ref List<ModCellEntry> __result)
            {
                if (__result == null)
                {
                    return;
                }

                SortState state = SortStates.GetValue(__instance, _ => new SortState());
                StringComparer comparer = StringComparer.OrdinalIgnoreCase;

                IEnumerable<ModCellEntry> ordered = state.Mode switch
                {
                    SortMode.Active => __result
                        .OrderByDescending(cell => cell.Enabled)
                        .ThenBy(cell => cell.Title ?? string.Empty, comparer),
                    _ => __result.OrderBy(cell => cell.Title ?? string.Empty, comparer)
                };

                __result = ordered.ToList();
            }

            [HarmonyPostfix]
            [HarmonyPatch("SwitchModStatus")]
            public static void SwitchModStatusPostfix(GuiScreenMods __instance)
            {
                RefreshList(__instance);
            }

            private static bool SetSortMode(GuiScreenMods screen, SortMode mode)
            {
                SortState state = SortStates.GetValue(screen, _ => new SortState());
                if (state.Mode == mode)
                {
                    return true;
                }

                state.Mode = mode;
                RefreshList(screen);
                return true;
            }

            private static void RefreshList(GuiScreenMods screen)
            {
                GuiComposer composer = screen.ElementComposer;
                if (composer == null)
                {
                    return;
                }

                ElementBounds listBounds = ListBoundsRef(screen);
                ElementBounds clippingBounds = ClippingBoundsRef(screen);
                if (listBounds == null || clippingBounds == null)
                {
                    return;
                }

                List<ModCellEntry> cells = InvokeLoadModCells(screen);
                GuiElementCellList<ModCellEntry> cellList = composer.GetCellList<ModCellEntry>("modstable");
                cellList?.ReloadCells(cells);

                listBounds.CalcWorldBounds();
                clippingBounds.CalcWorldBounds();

                GuiElementScrollbar scrollbar = composer.GetScrollbar("scrollbar");
                scrollbar?.SetHeights((float)clippingBounds.fixedHeight, (float)listBounds.fixedHeight);
            }

            private static List<ModCellEntry> InvokeLoadModCells(GuiScreenMods screen)
            {
                object? result = LoadModCellsMethod.Invoke(screen, Array.Empty<object>());
                return result as List<ModCellEntry>
                    ?? throw new InvalidOperationException("LoadModCells returned an unexpected value");
            }

            private static bool InvokeButtonHandler(GuiScreenMods screen, MethodInfo method)
            {
                object? result = method.Invoke(screen, Array.Empty<object>());
                return result is bool handled && handled;
            }
        }
    }
}
