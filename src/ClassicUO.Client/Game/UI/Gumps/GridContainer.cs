﻿#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static ClassicUO.Game.UI.Gumps.GridHightlightMenu;

namespace ClassicUO.Game.UI.Gumps
{
    internal class GridContainer : ResizableGump
    {
        private const int X_SPACING = 1, Y_SPACING = 1;
        private const int TOP_BAR_HEIGHT = 20;

        private static int _lastX = 100, _lastY = 100, _lastCorpseX = 100, _lastCorpseY = 100;
        private static int GRID_ITEM_SIZE { get { return (int)Math.Round(50 * (ProfileManager.CurrentProfile.GridContainersScale / 100f)); } }
        private static int BORDER_WIDTH = 4;
        private static int DEFAULT_WIDTH { get { return GetWidth(); } }
        private static int DEFAULT_HEIGHT { get { return GetHeight(); } }

        public readonly ushort OriginalContainerItemGraphic;
        private readonly AlphaBlendControl _background;
        private Item _container { get { return World.Items.Get(LocalSerial); } }
        private readonly Label _containerNameLabel;
        private readonly StbTextBox _searchBox;
        private readonly GumpPic _openRegularGump, _sortContents;
        private readonly ResizableStaticPic _quickDropBackpack;
        private readonly GumpPicTiled _backgroundTexture;
        private readonly NiceButton _setLootBag;
        private readonly bool isCorpse = false;

        private float _lastGridItemScale = (ProfileManager.CurrentProfile.GridContainersScale / 100f);
        private int _lastWidth = DEFAULT_WIDTH, _lastHeight = DEFAULT_HEIGHT;
        private bool quickLootThisContainer = false;
        private bool skipSave = false;

        private GridScrollArea _scrollArea;
        private GridSlotManager gridSlotManager;

        public bool? UseOldContainerStyle = null;

        public bool isPlayerBackpack = false;

        private string quickLootStatus { get { return ProfileManager.CurrentProfile.CorpseSingleClickLoot ? "<basefont color=\"green\">Enabled" : "<basefont color=\"red\">Disabled"; } }
        private string quickLootTooltip
        {
            get
            {
                if (isCorpse)
                    return $"Drop an item here to send it to your backpack.<br><br>Click this icon to enable/disable single-click looting for corpses.<br>   Currently {quickLootStatus}";
                else
                    return $"Drop an item here to send it to your backpack.<br><br>Click this icon to enable/disable single-click loot for this container while it remains open.<br>   Currently " + (quickLootThisContainer ? "<basefont color=\"green\">Enabled" : "<basefont color=\"red\">Disabled");
            }

        }

        public GridContainer(uint local, ushort originalContainerGraphic, bool? useGridStyle = null) : base(GetWidth(), GetHeight(), GetWidth(2), GetHeight(1), local, 0)
        {
            if (_container == null)
            {
                Dispose();
                return;
            }

            #region SET VARS
            isCorpse = _container.IsCorpse;
            if (useGridStyle != null)
                UseOldContainerStyle = !useGridStyle;
            if (LocalSerial == World.Player.FindItemByLayer(Layer.Backpack).Serial)
                isPlayerBackpack = true;

            Point savedSize, lastPos;
            if (isPlayerBackpack)
            {
                savedSize = ProfileManager.CurrentProfile.BackpackGridSize;
                lastPos = ProfileManager.CurrentProfile.BackpackGridPosition;
            }
            else
            {
                savedSize = GridSaveSystem.Instance.GetLastSize(LocalSerial);
                lastPos = GridSaveSystem.Instance.GetLastPosition(LocalSerial);
            }
            _lastWidth = Width = savedSize.X;
            _lastHeight = Height = savedSize.Y;

            if (isCorpse)
            {
                X = _lastCorpseX;
                Y = _lastCorpseY;
            }
            else
            {
                _lastX = X = lastPos.X;
                _lastY = Y = lastPos.Y;
            }

            AnchorType = ProfileManager.CurrentProfile.EnableGridContainerAnchor ? ANCHOR_TYPE.NONE : ANCHOR_TYPE.DISABLED;
            OriginalContainerItemGraphic = originalContainerGraphic;

            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            #endregion

            #region background
            _background = new AlphaBlendControl()
            {
                Width = Width - (BORDER_WIDTH * 2),
                Height = Height - (BORDER_WIDTH * 2),
                X = BORDER_WIDTH,
                Y = BORDER_WIDTH,
                Alpha = (float)ProfileManager.CurrentProfile.ContainerOpacity / 100,
                Hue = ProfileManager.CurrentProfile.Grid_UseContainerHue ? _container.Hue : ProfileManager.CurrentProfile.AltGridContainerBackgroundHue
            };

            _backgroundTexture = new GumpPicTiled(0);
            #endregion

            #region TOP BAR AREA
            _containerNameLabel = new Label(GetContainerName(), true, 0x0481)
            {
                X = BORDER_WIDTH,
                Y = -20
            };

            _searchBox = new StbTextBox(0xFF, 20, 150, true, FontStyle.None, 0x0481)
            {
                X = BORDER_WIDTH,
                Y = BORDER_WIDTH,
                Multiline = false,
                Width = 150,
                Height = 20
            };
            _searchBox.TextChanged += (sender, e) => { updateItems(); };
            _searchBox.DragBegin += (sender, e) => { InvokeDragBegin(e.Location); };

            var regularGumpIcon = GumpsLoader.Instance.GetGumpTexture(5839, out var bounds);
            _openRegularGump = new GumpPic(_background.Width - 25 - BORDER_WIDTH, BORDER_WIDTH, regularGumpIcon == null ? (ushort)1209 : (ushort)5839, 0);
            _openRegularGump.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    UseOldContainerStyle = true;
                    OpenOldContainer(LocalSerial);
                }
            };
            _openRegularGump.MouseEnter += (sender, e) => { _openRegularGump.Graphic = regularGumpIcon == null ? (ushort)1210 : (ushort)5840; };
            _openRegularGump.MouseExit += (sender, e) => { _openRegularGump.Graphic = regularGumpIcon == null ? (ushort)1209 : (ushort)5839; };
            _openRegularGump.SetTooltip("Open the original style container.");

            var quickDropIcon = GumpsLoader.Instance.GetGumpTexture(1625, out var bounds1);
            _quickDropBackpack = new ResizableStaticPic(World.Player.FindItemByLayer(Layer.Backpack).DisplayedGraphic, 20, 20)
            {
                X = Width - _openRegularGump.Width - 20 - BORDER_WIDTH,
                Y = BORDER_WIDTH
            };
            _quickDropBackpack.MouseUp += (sender, e) =>
            {
                if (e.Button == MouseButtonType.Left && _quickDropBackpack.MouseIsOver)
                {
                    if (Client.Game.GameCursor.ItemHold.Enabled)
                    {
                        GameActions.DropItem(Client.Game.GameCursor.ItemHold.Serial, 0xFFFF, 0xFFFF, 0, World.Player.FindItemByLayer(Layer.Backpack));
                    }
                    else if (isCorpse)
                    {
                        ProfileManager.CurrentProfile.CorpseSingleClickLoot ^= true;
                        _quickDropBackpack.SetTooltip(quickLootTooltip);
                    }
                    else
                    {
                        quickLootThisContainer ^= true;
                        _quickDropBackpack.SetTooltip(quickLootTooltip);
                    }
                }
                else if (e.Button == MouseButtonType.Right)
                {
                    InvokeMouseCloseGumpWithRClick();
                }
            };
            _quickDropBackpack.MouseEnter += (sender, e) => { _quickDropBackpack.Hue = 0x34; };
            _quickDropBackpack.MouseExit += (sender, e) => { _quickDropBackpack.Hue = 0; };
            _quickDropBackpack.SetTooltip(quickLootTooltip);

            _sortContents = new GumpPic(_quickDropBackpack.X - 20, BORDER_WIDTH, 1210, 0);
            _sortContents.MouseUp += (sender, e) => { updateItems(true); };
            _sortContents.MouseEnter += (sender, e) => { _sortContents.Graphic = 1209; };
            _sortContents.MouseExit += (sender, e) => { _sortContents.Graphic = 1210; };
            _sortContents.SetTooltip("Sort this container.");
            #endregion

            #region Scroll Area
            _scrollArea = new GridScrollArea(
                _background.X,
                TOP_BAR_HEIGHT + _background.Y,
                _background.Width,
                _background.Height - (_containerNameLabel.Height + 1)
                );

            _scrollArea.MouseUp += _scrollArea_MouseUp;
            _scrollArea.DragBegin += _scrollArea_DragBegin;
            #endregion

            #region Set loot bag
            _setLootBag = new NiceButton(0, Height - 20, 100, 20, ButtonAction.Default, "Set loot bag") { IsSelectable = false };
            _setLootBag.IsVisible = isCorpse;
            _setLootBag.SetTooltip("For double click looting only");
            _setLootBag.MouseUp += (s, e) =>
            {
                GameActions.Print(Resources.ResGumps.TargetContainerToGrabItemsInto);
                TargetManager.SetTargeting(CursorTarget.SetGrabBag, 0, TargetType.Neutral);
            };
            #endregion

            #region Add controls
            Add(_background);
            Add(_backgroundTexture);
            Add(_containerNameLabel);
            _searchBox.Add(new AlphaBlendControl(0.5f)
            {
                Hue = 0x0481,
                Width = _searchBox.Width,
                Height = _searchBox.Height
            });
            Add(_searchBox);
            Add(_openRegularGump);
            Add(_quickDropBackpack);
            Add(_sortContents);
            Add(_scrollArea);
            Add(_setLootBag);
            #endregion

            gridSlotManager = new GridSlotManager(LocalSerial, this, _scrollArea); //Must come after scroll area

            if (GridSaveSystem.Instance.UseOriginalContainerGump(LocalSerial) && (UseOldContainerStyle == null || UseOldContainerStyle == true))
            {
                skipSave = true; //Avoid unsaving item slots because they have not be set up yet
                OpenOldContainer(local);
                return;
            }

            BuildBorder();
            ResizeWindow(savedSize);
        }

        public override GumpType GumpType => GumpType.GridContainer;

        private static int GetWidth(int columns = -1)
        {
            if (columns < 0)
                columns = ProfileManager.CurrentProfile.Grid_DefaultColumns;
            return (BORDER_WIDTH * 2)     //The borders around the container, one on the left and one on the right
            + 15                   //The width of the scroll bar
            + (GRID_ITEM_SIZE * columns) //How many items to fit in left to right
            + (X_SPACING * columns);      //Spacing between each grid item(x columns)
        }

        private static int GetHeight(int rows = -1)
        {
            if (rows < 0)
                rows = ProfileManager.CurrentProfile.Grid_DefaultRows;
            return TOP_BAR_HEIGHT + (BORDER_WIDTH * 2) + ((GRID_ITEM_SIZE + Y_SPACING) * rows);
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);

            GridSaveSystem.Instance.SaveContainer(LocalSerial, gridSlotManager.GridSlots, Width, Height, X, Y);

            if (isPlayerBackpack)
            {
                ProfileManager.CurrentProfile.BackpackGridPosition = Location;
                ProfileManager.CurrentProfile.BackpackGridSize = new Point(Width, Height);
            }

            writer.WriteAttributeString("ogContainer", OriginalContainerItemGraphic.ToString());
            writer.WriteAttributeString("width", Width.ToString());
            writer.WriteAttributeString("height", Height.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
            int rW = int.Parse(xml.GetAttribute("width"));
            int rH = int.Parse(xml.GetAttribute("height"));

            if (isPlayerBackpack)
            {
                rW = ProfileManager.CurrentProfile.BackpackGridSize.X;
                rH = ProfileManager.CurrentProfile.BackpackGridSize.Y;
            }

            ResizeWindow(new Point(rW, rH));
            //InvalidateContents = true;
        }

        private void _scrollArea_DragBegin(object sender, MouseEventArgs e)
        {
            InvokeDragBegin(e.Location);
        }

        private void _scrollArea_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtonType.Left && _scrollArea.MouseIsOver)
            {
                if (Client.Game.GameCursor.ItemHold.Enabled)
                {
                    GameActions.DropItem(Client.Game.GameCursor.ItemHold.Serial, 0xFFFF, 0xFFFF, 0, LocalSerial);
                }
                else if (TargetManager.IsTargeting)
                {
                    TargetManager.Target(LocalSerial);
                }
            }
            else if (e.Button == MouseButtonType.Right)
            {
                InvokeMouseCloseGumpWithRClick();
            }
        }

        private void OpenOldContainer(uint serial)
        {
            ContainerGump container;

            UIManager.GetGump<ContainerGump>(serial)?.Dispose();

            ushort graphic = OriginalContainerItemGraphic;
            if (Client.Version >= Utility.ClientVersion.CV_706000 && ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.UseLargeContainerGumps)
            {
                GumpsLoader loader = GumpsLoader.Instance;

                switch (graphic)
                {
                    case 0x0048:
                        if (loader.GetGumpTexture(0x06E8, out _) != null)
                        {
                            graphic = 0x06E8;
                        }

                        break;

                    case 0x0049:
                        if (loader.GetGumpTexture(0x9CDF, out _) != null)
                        {
                            graphic = 0x9CDF;
                        }

                        break;

                    case 0x0051:
                        if (loader.GetGumpTexture(0x06E7, out _) != null)
                        {
                            graphic = 0x06E7;
                        }

                        break;

                    case 0x003E:
                        if (loader.GetGumpTexture(0x06E9, out _) != null)
                        {
                            graphic = 0x06E9;
                        }

                        break;

                    case 0x004D:
                        if (loader.GetGumpTexture(0x06EA, out _) != null)
                        {
                            graphic = 0x06EA;
                        }

                        break;

                    case 0x004E:
                        if (loader.GetGumpTexture(0x06E6, out _) != null)
                        {
                            graphic = 0x06E6;
                        }

                        break;

                    case 0x004F:
                        if (loader.GetGumpTexture(0x06E5, out _) != null)
                        {
                            graphic = 0x06E5;
                        }

                        break;

                    case 0x004A:
                        if (loader.GetGumpTexture(0x9CDD, out _) != null)
                        {
                            graphic = 0x9CDD;
                        }

                        break;

                    case 0x0044:
                        if (loader.GetGumpTexture(0x9CE3, out _) != null)
                        {
                            graphic = 0x9CE3;
                        }

                        break;
                }
            }

            ContainerManager.CalculateContainerPosition(serial, graphic);

            container = new ContainerGump(_container.Serial, graphic, true, true)
            {
                X = ContainerManager.X,
                Y = ContainerManager.Y,
                InvalidateContents = true
            };
            UIManager.Add(container);
            Dispose();
        }

        private void updateItems(bool overrideSort = false)
        {
            //Container doesn't exist or has no items
            if (_container == null)
            {
                Dispose();
                return;
            }

            List<Item> sortedContents = ProfileManager.CurrentProfile.GridContainerSearchMode == 0 ? gridSlotManager.SearchResults(_searchBox.Text) : GridSlotManager.GetItemsInContainer(_container);
            gridSlotManager.RebuildContainer(sortedContents, _searchBox.Text, overrideSort);
            _containerNameLabel.Text = GetContainerName();
            InvalidateContents = false;
        }

        protected override void UpdateContents()
        {
            if (InvalidateContents && !IsDisposed && IsVisible)
            {
                if (ProfileManager.CurrentProfile != null)
                    _background.Alpha = (float)ProfileManager.CurrentProfile.ContainerOpacity / 100;
                updateItems();
            }
        }

        protected override void OnMouseExit(int x, int y)
        {
            if (isCorpse && _container != null && !_container.IsDestroyed)
            {
                SelectedObject.CorpseObject = null;
            }
        }

        public override void Dispose()
        {
            if (isCorpse)
            {
                _lastCorpseX = X;
                _lastCorpseY = Y;
            }
            else
            {
                _lastX = X;
                _lastY = Y;
            }

            if (_container != null)
            {
                if (_container == SelectedObject.CorpseObject)
                {
                    SelectedObject.CorpseObject = null;
                }
            }

            if (gridSlotManager != null && !skipSave)
                if (gridSlotManager.ItemPositions.Count > 0 && !isCorpse)
                    GridSaveSystem.Instance.SaveContainer(LocalSerial, gridSlotManager.GridSlots, Width, Height, X, Y, UseOldContainerStyle);

            base.Dispose();
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
                return;


            Item item = World.Items.Get(LocalSerial);

            if (item == null || item.IsDestroyed)
            {
                Dispose();

                return;
            }

            if (item.IsCorpse)
            {
                if (item.Distance > 3)
                {
                    Dispose();
                    return;
                }
            }

            if ((_lastWidth != Width || _lastHeight != Height) || _lastGridItemScale != GRID_ITEM_SIZE)
            {
                _lastGridItemScale = GRID_ITEM_SIZE;
                _background.Width = Width - (BORDER_WIDTH * 2);
                _background.Height = Height - (BORDER_WIDTH * 2);
                _scrollArea.Width = _background.Width;
                _scrollArea.Height = _background.Height - TOP_BAR_HEIGHT;
                _openRegularGump.X = Width - _openRegularGump.Width - BORDER_WIDTH;
                _quickDropBackpack.X = _openRegularGump.X - _quickDropBackpack.Width;
                _sortContents.X = _quickDropBackpack.X - _sortContents.Width;
                _lastHeight = Height;
                _lastWidth = Width;
                _searchBox.Width = Math.Min(Width - (BORDER_WIDTH * 2) - _openRegularGump.Width - _quickDropBackpack.Width - _sortContents.Width, 150);
                _backgroundTexture.Width = _background.Width;
                _backgroundTexture.Height = _background.Height;
                _backgroundTexture.Alpha = _background.Alpha;
                _backgroundTexture.Hue = _background.Hue;
                _setLootBag.Y = Height - 20;
                if (isPlayerBackpack)
                    ProfileManager.CurrentProfile.BackpackGridSize = new Point(Width, Height);
                RequestUpdateContents();
            }

            if (isPlayerBackpack)
                if (Location != ProfileManager.CurrentProfile.BackpackGridPosition)
                    ProfileManager.CurrentProfile.BackpackGridPosition = Location;


            if (item != null && !item.IsDestroyed && UIManager.MouseOverControl != null && (UIManager.MouseOverControl == this || UIManager.MouseOverControl.RootParent == this))
            {
                SelectedObject.Object = item;
                if (item.IsCorpse)
                    SelectedObject.CorpseObject = item;
            }
        }

        private string GetContainerName()
        {
            return _container.Name?.Length > 0 ? _container.Name : "a container";
        }

        public void OptionsUpdated()
        {
            var newAlpha = (float)ProfileManager.CurrentProfile.ContainerOpacity / 100;
            var newHue = ProfileManager.CurrentProfile.Grid_UseContainerHue ? _container.Hue : ProfileManager.CurrentProfile.AltGridContainerBackgroundHue;
            _background.Hue = newHue;
            _background.Alpha = newAlpha;
            _backgroundTexture.Alpha = _background.Alpha;
            _backgroundTexture.Hue = _background.Hue;
            BorderControl.Hue = _background.Hue;
            BorderControl.Alpha = _background.Alpha;
        }

        public void BuildBorder()
        {
            int graphic = 0, borderSize = 0;
            switch ((BorderStyle)ProfileManager.CurrentProfile.Grid_BorderStyle)
            {
                case BorderStyle.Style1:
                    graphic = 3500; borderSize = 26;
                    break;
                case BorderStyle.Style2:
                    graphic = 5054; borderSize = 12;
                    break;
                case BorderStyle.Style3:
                    graphic = 5120; borderSize = 10;
                    break;
                case BorderStyle.Style4:
                    graphic = 9200; borderSize = 7;
                    break;
                case BorderStyle.Style5:
                    graphic = 9270; borderSize = 10;
                    break;
                case BorderStyle.Style6:
                    graphic = 9300; borderSize = 4;
                    break;
                case BorderStyle.Style7:
                    graphic = 9260; borderSize = 17;
                    break;
                case BorderStyle.Style8:
                    if (GumpsLoader.Instance.GetGumpTexture(40303, out var bounds) != null)
                        graphic = 40303;
                    else
                        graphic = 83;
                    borderSize = 16;
                    break;

                default:
                case BorderStyle.Default:
                    BorderControl.DefaultGraphics();
                    _backgroundTexture.IsVisible = false;
                    _background.IsVisible = true;
                    BORDER_WIDTH = 4;
                    break;
            }

            if ((BorderStyle)ProfileManager.CurrentProfile.Grid_BorderStyle != BorderStyle.Default)
            {
                BorderControl.T_Left = (ushort)graphic;
                BorderControl.H_Border = (ushort)(graphic + 1);
                BorderControl.T_Right = (ushort)(graphic + 2);
                BorderControl.V_Border = (ushort)(graphic + 3);

                _backgroundTexture.Graphic = (ushort)(graphic + 4);
                _backgroundTexture.IsVisible = true;
                _backgroundTexture.Hue = _background.Hue;
                BorderControl.Hue = _background.Hue;
                BorderControl.Alpha = _background.Alpha;
                _background.IsVisible = false;

                BorderControl.V_Right_Border = (ushort)(graphic + 5);
                BorderControl.B_Left = (ushort)(graphic + 6);
                BorderControl.H_Bottom_Border = (ushort)(graphic + 7);
                BorderControl.B_Right = (ushort)(graphic + 8);
                BorderControl.BorderSize = borderSize;
                BORDER_WIDTH = borderSize;
            }
            RePosition();
            OnResize();
        }

        public void RePosition()
        {
            _background.X = BORDER_WIDTH;
            _background.Y = BORDER_WIDTH;
            _scrollArea.X = _background.X;
            _scrollArea.Y = TOP_BAR_HEIGHT + _background.Y;
            _searchBox.Y = BORDER_WIDTH;
            _quickDropBackpack.Y = BORDER_WIDTH;
            _sortContents.Y = BORDER_WIDTH;
            _openRegularGump.Y = BORDER_WIDTH;
            _searchBox.X = BORDER_WIDTH;
            _backgroundTexture.X = _background.X;
            _backgroundTexture.Y = _background.Y;
            _backgroundTexture.Width = Width - (BORDER_WIDTH * 2);
            _backgroundTexture.Height = Height - (BORDER_WIDTH * 2);
            _background.Width = Width - (BORDER_WIDTH * 2);
            _background.Height = Height - (BORDER_WIDTH * 2);
            _scrollArea.Width = _background.Width;
            _scrollArea.Height = _background.Height - TOP_BAR_HEIGHT;
        }

        public enum BorderStyle
        {
            Default,
            Style1,
            Style2,
            Style3,
            Style4,
            Style5,
            Style6,
            Style7,
            Style8
        }

        public static void Clear()
        {
            GridSaveSystem.Instance.Clear();
        }

        private class GridItem : Control
        {
            private readonly HitBox hit;
            private bool mousePressedWhenEntered = false;
            private readonly Item container;
            private Item _item;
            private readonly GridContainer gridContainer;
            public bool ItemGridLocked = false;
            private readonly int slot;
            private GridContainerPreview preview;
            Label count;
            AlphaBlendControl background;
            private CustomToolTip toolTipThis, toolTipitem1, toolTipitem2;

            private bool borderHighlight = false;
            private ushort borderHighlightHue = 0;

            public bool Hightlight = false;
            public bool SelectHighlight = false;
            public Item SlotItem { get { return _item; } set { _item = value; LocalSerial = value.Serial; } }

            private readonly int[] spellbooks = { 0x0EFA, 0x2253, 0x2252, 0x238C, 0x23A0, 0x2D50, 0x2D9D, 0x225A };

            public GridItem(uint serial, int size, Item _container, GridContainer gridContainer, int count)
            {
                #region VARS
                slot = count;
                container = _container;
                this.gridContainer = gridContainer;
                LocalSerial = serial;
                _item = World.Items.Get(serial);
                CanMove = true;
                if (_item != null)
                {
                    texture = ArtLoader.Instance.GetStaticTexture(_item.DisplayedGraphic, out bounds);
                    rect = ArtLoader.Instance.GetRealArtBounds(_item.DisplayedGraphic);
                }
                #endregion

                background = new AlphaBlendControl(0.25f);
                background.Width = size;
                background.Height = size;
                Width = Height = size;
                Add(background);

                hit = new HitBox(0, 0, size, size, null, 0f);
                Add(hit);

                SetGridItem(_item);

                hit.MouseEnter += _hit_MouseEnter;
                hit.MouseExit += _hit_MouseExit;
                hit.MouseUp += _hit_MouseUp;
                hit.MouseDoubleClick += _hit_MouseDoubleClick;
            }

            public void SetHighLightBorder(ushort hue)
            {
                borderHighlight = hue == 0 ? false : true;
                borderHighlightHue = hue;
            }

            public void Resize()
            {
                Width = GRID_ITEM_SIZE;
                Height = GRID_ITEM_SIZE;
                hit.Width = GRID_ITEM_SIZE;
                hit.Height = GRID_ITEM_SIZE;
                background.Width = GRID_ITEM_SIZE;
                background.Height = GRID_ITEM_SIZE;
            }

            public void SetGridItem(Item item)
            {
                if (item == null)
                {
                    _item = null;
                    LocalSerial = 0;
                    hit.ClearTooltip();
                    Hightlight = false;
                    count = null;
                    ItemGridLocked = false;
                }
                else
                {
                    texture = ArtLoader.Instance.GetStaticTexture(item.DisplayedGraphic, out bounds);
                    rect = ArtLoader.Instance.GetRealArtBounds(item.DisplayedGraphic);

                    _item = item;
                    LocalSerial = item.Serial;
                    int itemAmt = (_item.ItemData.IsStackable ? _item.Amount : 1);
                    if (itemAmt > 1)
                    {
                        count = new Label(itemAmt.ToString(), true, 0x0481, align: TEXT_ALIGN_TYPE.TS_LEFT, maxwidth: Width - 3);
                        count.X = 1;
                        count.Y = Height - count.Height;
                    }
                    if (MultiItemMoveGump.MoveItems.Contains(_item))
                        Hightlight = true;
                    hit.SetTooltip(_item);
                }
            }

            private void _hit_MouseDoubleClick(object sender, MouseDoubleClickEventArgs e)
            {
                if (e.Button != MouseButtonType.Left || TargetManager.IsTargeting || _item == null)
                {
                    return;
                }
                if (!Keyboard.Ctrl && (ProfileManager.CurrentProfile.DoubleClickToLootInsideContainers && gridContainer.isCorpse) && !_item.IsDestroyed && !_item.ItemData.IsContainer && container != World.Player.FindItemByLayer(Layer.Backpack) && !_item.IsLocked && _item.IsLootable)
                {
                    GameActions.GrabItem(_item, _item.Amount);
                }
                else
                {
                    GameActions.DoubleClick(LocalSerial);
                }
                e.Result = true;
            }

            private void _hit_MouseUp(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtonType.Left)
                {
                    if (Client.Game.GameCursor.ItemHold.Enabled)
                    {
                        if (_item != null && _item.ItemData.IsContainer)
                        {
                            Rectangle containerBounds = ContainerManager.Get(_item.Graphic).Bounds;
                            GameActions.DropItem(Client.Game.GameCursor.ItemHold.Serial, 0xFFFF, 0xFFFF, 0, _item.Serial);
                            Mouse.CancelDoubleClick = true;
                        }
                        else if (_item != null && _item.ItemData.IsStackable && _item.Graphic == Client.Game.GameCursor.ItemHold.Graphic)
                        {
                            GameActions.DropItem(Client.Game.GameCursor.ItemHold.Serial, _item.X, _item.Y, 0, _item.Serial);
                            Mouse.CancelDoubleClick = true;
                        }
                        else
                        {
                            Rectangle containerBounds = ContainerManager.Get(container.Graphic).Bounds;
                            gridContainer.gridSlotManager.AddLockedItemSlot(Client.Game.GameCursor.ItemHold.Serial, slot);
                            GameActions.DropItem(Client.Game.GameCursor.ItemHold.Serial, containerBounds.X / 2, containerBounds.Y / 2, 0, container.Serial);
                            Mouse.CancelDoubleClick = true;
                        }
                    }
                    else if (TargetManager.IsTargeting)
                    {
                        if (_item != null)
                        {
                            TargetManager.Target(_item);
                            if (TargetManager.TargetingState == CursorTarget.SetTargetClientSide)
                            {
                                UIManager.Add(new InspectorGump(_item));
                            }
                        }
                        else
                            TargetManager.Target(container);
                        Mouse.CancelDoubleClick = true;
                    }
                    else if (Keyboard.Ctrl)
                    {
                        gridContainer.gridSlotManager.SetLockedSlot(slot, !ItemGridLocked);
                        Mouse.CancelDoubleClick = true;
                    }
                    else if (Keyboard.Alt && _item != null)
                    {
                        if (!MultiItemMoveGump.MoveItems.Contains(_item))
                            MultiItemMoveGump.MoveItems.Enqueue(_item);
                        MultiItemMoveGump.AddMultiItemMoveGumpToUI(gridContainer.X - 200, gridContainer.Y);
                        SelectHighlight = true;
                        Mouse.CancelDoubleClick = true;
                    }
                    else if (_item != null)
                    {
                        Point offset = Mouse.LDragOffset;
                        if (Math.Abs(offset.X) < Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS && Math.Abs(offset.Y) < Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS)
                        {
                            if ((gridContainer.isCorpse && ProfileManager.CurrentProfile.CorpseSingleClickLoot) || gridContainer.quickLootThisContainer)
                            {
                                GameActions.GrabItem(_item.Serial, _item.Amount);
                                Mouse.CancelDoubleClick = true;
                            }
                            else
                                DelayedObjectClickManager.Set(_item.Serial, gridContainer.X, gridContainer.Y - 80, Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK);
                        }
                    }
                }
            }

            private void _hit_MouseExit(object sender, MouseEventArgs e)
            {
                if (Mouse.LButtonPressed && !mousePressedWhenEntered)
                {
                    Point offset = Mouse.LDragOffset;
                    if (Math.Abs(offset.X) >= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS || Math.Abs(offset.Y) >= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS)
                    {
                        if (_item != null)
                        {
                            if (!Keyboard.Alt)
                                GameActions.PickUp(_item, e.X, e.Y);
                        }
                        else
                        {
                            if (ProfileManager.CurrentProfile.HoldAltToMoveGumps)
                            {
                                if (Keyboard.Alt)
                                    UIManager.AttemptDragControl(gridContainer);
                            }
                            else
                                UIManager.AttemptDragControl(gridContainer);
                        }
                    }
                }

                if (Keyboard.Alt && Mouse.LButtonPressed && _item != null)
                {
                    if (!MultiItemMoveGump.MoveItems.Contains(_item))
                        MultiItemMoveGump.MoveItems.Enqueue(_item);
                    MultiItemMoveGump.AddMultiItemMoveGumpToUI(gridContainer.X - 200, gridContainer.Y);
                    SelectHighlight = true;
                }


                GridContainerPreview g;
                while ((g = UIManager.GetGump<GridContainerPreview>()) != null)
                {
                    g.Dispose();
                }
            }

            private void _hit_MouseEnter(object sender, MouseEventArgs e)
            {
                SelectedObject.Object = World.Get(LocalSerial);
                if (Mouse.LButtonPressed)
                    mousePressedWhenEntered = true;
                else
                    mousePressedWhenEntered = false;
                if (_item != null)
                {
                    if (_item.ItemData.IsContainer && _item.Items != null && ProfileManager.CurrentProfile.GridEnableContPreview && !spellbooks.Contains(_item.Graphic))
                    {
                        preview = new GridContainerPreview(_item, Mouse.Position.X, Mouse.Position.Y);
                        UIManager.Add(preview);
                    }

                    if (Keyboard.Alt && Mouse.LButtonPressed && _item != null)
                    {
                        if (!MultiItemMoveGump.MoveItems.Contains(_item))
                            MultiItemMoveGump.MoveItems.Enqueue(_item);
                        MultiItemMoveGump.AddMultiItemMoveGumpToUI(gridContainer.X - 200, gridContainer.Y);
                        SelectHighlight = true;
                    }

                    if (!hit.HasTooltip)
                        hit.SetTooltip(_item);
                }
            }

            private Texture2D texture;
            private Rectangle rect;
            private Rectangle bounds;

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (_item != null && _item.ItemData.Layer > 0 && hit.MouseIsOver && Keyboard.Ctrl && (toolTipThis == null || toolTipThis.IsDisposed) && (toolTipitem1 == null || toolTipitem1.IsDisposed) && (toolTipitem2 == null || toolTipitem2.IsDisposed))
                {
                    Item compItem = World.Player.FindItemByLayer((Layer)_item.ItemData.Layer);
                    if (compItem != null && (Layer)_item.ItemData.Layer != Layer.Backpack)
                    {
                        hit.ClearTooltip();
                        List<CustomToolTip> toolTipList = new List<CustomToolTip>();
                        toolTipThis = new CustomToolTip(_item, Mouse.Position.X + 5, Mouse.Position.Y + 5, hit);
                        toolTipList.Add(toolTipThis);
                        //UIManager.Add(toolTipThis);
                        toolTipitem1 = new CustomToolTip(compItem, toolTipThis.X + toolTipThis.Width + 10, toolTipThis.Y, hit, "<basefont color=\"orange\">Equipped Item<br>");
                        //UIManager.Add(toolTipitem1);
                        toolTipList.Add(toolTipitem1);

                        if ((Layer)_item.ItemData.Layer == Layer.OneHanded)
                        {
                            Item compItem2 = World.Player.FindItemByLayer(Layer.TwoHanded);
                            if (compItem2 != null)
                            {
                                toolTipitem2 = new CustomToolTip(compItem2, toolTipitem1.X + toolTipitem1.Width + 10, toolTipitem1.Y, hit, "<basefont color=\"orange\">Equipped Item<br>");
                                //UIManager.Add(toolTipitem2);
                                toolTipList.Add(toolTipitem2);
                            }
                        }
                        else if ((Layer)_item.ItemData.Layer == Layer.TwoHanded)
                        {
                            Item compItem2 = World.Player.FindItemByLayer(Layer.OneHanded);
                            if (compItem2 != null)
                            {
                                toolTipitem2 = new CustomToolTip(compItem2, toolTipitem1.X + toolTipitem1.Width + 10, toolTipitem1.Y, hit, "<basefont color=\"orange\">Equipped Item<br>");
                                //UIManager.Add(toolTipitem2);
                                toolTipList.Add(toolTipitem2);
                            }
                        }

                        MultipleToolTipGump multipleToolTipGump = new MultipleToolTipGump(Mouse.Position.X + 10, Mouse.Position.Y + 10, toolTipList.ToArray(), hit);
                        UIManager.Add(multipleToolTipGump);
                    }
                }

                if (SelectHighlight)
                    if (!MultiItemMoveGump.MoveItems.Contains(_item))
                        SelectHighlight = false;

                base.Draw(batcher, x, y);

                Vector3 hueVector;

                hueVector = ShaderHueTranslator.GetHueVector(ProfileManager.CurrentProfile.GridBorderHue, false, (float)ProfileManager.CurrentProfile.GridBorderAlpha / 100);

                if (ItemGridLocked)
                    hueVector = ShaderHueTranslator.GetHueVector(0x2, false, (float)ProfileManager.CurrentProfile.GridBorderAlpha / 100);
                if (Hightlight || SelectHighlight)
                {
                    hueVector = ShaderHueTranslator.GetHueVector(0x34, false, 1);
                }

                batcher.DrawRectangle
                (
                    SolidColorTextureCache.GetTexture(Color.White),
                    x,
                    y,
                    Width,
                    Height,
                    hueVector
                );

                if (borderHighlight)
                    batcher.DrawRectangle
                    (
                        SolidColorTextureCache.GetTexture(Color.White),
                        x + 6,
                        y + 6,
                        Width - 12,
                        Height - 12,
                        ShaderHueTranslator.GetHueVector(borderHighlightHue, false, 0.8f)
                    );

                if (hit.MouseIsOver && _item != null)
                {
                    hueVector.Z = 0.3f;

                    batcher.Draw
                    (
                        SolidColorTextureCache.GetTexture(Color.White),
                        new Rectangle
                        (
                            x + 1,
                            y,
                            Width - 1,
                            Height
                        ),
                        hueVector
                    );
                }

                if (_item != null && texture != null & rect != null)
                {
                    hueVector = ShaderHueTranslator.GetHueVector(_item.Hue, _item.ItemData.IsPartialHue, 1f);

                    Point originalSize = new Point(hit.Width, hit.Height);
                    Point point = new Point();
                    var scale = (ProfileManager.CurrentProfile.GridContainersScale / 100f);

                    if (rect.Width < hit.Width)
                    {
                        if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                            originalSize.X = (ushort)(rect.Width * scale);
                        else
                            originalSize.X = rect.Width;

                        point.X = (hit.Width >> 1) - (originalSize.X >> 1);
                    }
                    else if (rect.Width > hit.Width)
                    {
                        if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                            originalSize.X = (ushort)(hit.Width * scale);
                        else
                            originalSize.X = hit.Width;
                        point.X = (hit.Width >> 1) - (originalSize.X >> 1);
                    }

                    if (rect.Height < hit.Height)
                    {
                        if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                            originalSize.Y = (ushort)(rect.Height * scale);
                        else
                            originalSize.Y = rect.Height;

                        point.Y = (hit.Height >> 1) - (originalSize.Y >> 1);
                    }
                    else if (rect.Height > hit.Height)
                    {
                        if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                            originalSize.Y = (ushort)(hit.Height * scale);
                        else
                            originalSize.Y = hit.Height;

                        point.Y = (hit.Height >> 1) - (originalSize.Y >> 1);
                    }

                    batcher.Draw
                    (
                        texture,
                        new Rectangle
                        (
                            x + point.X,
                            y + point.Y + hit.Y,
                            originalSize.X,
                            originalSize.Y
                        ),
                        new Rectangle
                        (
                            bounds.X + rect.X,
                            bounds.Y + rect.Y,
                            rect.Width,
                            rect.Height
                        ),
                        hueVector
                    );
                    if (count != null)
                        count.Draw(batcher, x + count.X, y + count.Y);
                }
                return true;
            }
        }

        private class GridSlotManager
        {
            private Dictionary<int, GridItem> gridSlots = new Dictionary<int, GridItem>();
            private Item container;
            private List<Item> containerContents;
            private int amount = 125;
            private Control area;
            private Dictionary<int, uint> itemPositions = new Dictionary<int, uint>();
            private List<uint> itemLocks = new List<uint>();

            public Dictionary<int, GridItem> GridSlots { get { return gridSlots; } }
            public List<Item> ContainerContents { get { return containerContents; } }
            public Dictionary<int, uint> ItemPositions { get { return itemPositions; } }


            public GridSlotManager(uint thisContainer, GridContainer gridContainer, Control controlArea)
            {
                #region VARS
                area = controlArea;
                foreach (GridSaveSystem.GridItemSlotSaveData item in GridSaveSystem.Instance.GetItemSlots(thisContainer))
                {
                    ItemPositions.Add(item.Slot, item.Serial);
                    if (item.IsLocked)
                        itemLocks.Add(item.Serial);

                }
                container = World.Items.Get(thisContainer);
                UpdateItems();
                if (containerContents.Count > 125)
                    amount = containerContents.Count;
                #endregion

                for (int i = 0; i < amount; i++)
                {
                    GridItem GI = new GridItem(0, GRID_ITEM_SIZE, container, gridContainer, i);
                    gridSlots.Add(i, GI);
                    area.Add(GI);
                }
            }

            public void AddLockedItemSlot(uint serial, int specificSlot)
            {
                if (ItemPositions.Values.Contains(serial)) //Is this item already locked? Lets remove it from lock status for now
                {
                    int removeSlot = ItemPositions.First((x) => x.Value == serial).Key;
                    ItemPositions.Remove(removeSlot);
                }

                if (ItemPositions.ContainsKey(specificSlot)) //Is the slot they wanted this item in already taken? Lets remove that item
                    ItemPositions.Remove(specificSlot);
                ItemPositions.Add(specificSlot, serial); //Now we add this item at the desired slot
            }

            public void RebuildContainer(List<Item> filteredItems, string searchText = "", bool overrideSort = false)
            {
                foreach (var slot in gridSlots)
                {
                    slot.Value.SetGridItem(null);
                }

                foreach (var spot in itemPositions)
                {
                    Item i = World.Items.Get(spot.Value);
                    if (i != null)
                        if (filteredItems.Contains(i) && (!overrideSort || itemLocks.Contains(spot.Value)))
                        {
                            if (spot.Key < gridSlots.Count)
                            {
                                gridSlots[spot.Key].SetGridItem(i);

                                if (itemLocks.Contains(spot.Value))
                                    gridSlots[spot.Key].ItemGridLocked = true;

                                filteredItems.Remove(i);
                            }
                        }
                }

                foreach (Item i in filteredItems)
                {
                    foreach (var slot in gridSlots)
                    {
                        if (slot.Value.SlotItem != null)
                            continue;
                        slot.Value.SetGridItem(i);
                        AddLockedItemSlot(i, slot.Key);
                        break;
                    }
                }

                foreach (var slot in gridSlots)
                {
                    slot.Value.IsVisible = !(!string.IsNullOrWhiteSpace(searchText) && ProfileManager.CurrentProfile.GridContainerSearchMode == 0);
                    if (slot.Value.SlotItem != null && !string.IsNullOrWhiteSpace(searchText))
                    {
                        if (SearchItemNameAndProps(searchText, slot.Value.SlotItem))
                        {
                            slot.Value.Hightlight = ProfileManager.CurrentProfile.GridContainerSearchMode == 1;
                            slot.Value.IsVisible = true;
                        }
                    }
                }
                SetGridPositions();
                ApplyHighlightProperties();
            }

            public void SetLockedSlot(int slot, bool locked)
            {
                if (gridSlots[slot].SlotItem == null)
                    return;
                gridSlots[slot].ItemGridLocked = locked;
                if (!locked)
                    itemLocks.Remove(gridSlots[slot].SlotItem);
                else
                    itemLocks.Add(gridSlots[slot].SlotItem);
            }

            private void SetGridPositions()
            {
                int x = X_SPACING, y = 0;
                foreach (var slot in gridSlots)
                {
                    if (!slot.Value.IsVisible)
                    {
                        continue;
                    }
                    if (x + GRID_ITEM_SIZE >= area.Width - 14) //14 is the scroll bar width
                    {
                        x = X_SPACING;
                        y += GRID_ITEM_SIZE + Y_SPACING;
                    }
                    slot.Value.X = x;
                    slot.Value.Y = y;
                    slot.Value.Resize();
                    x += GRID_ITEM_SIZE + X_SPACING;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="search"></param>
            /// <returns>List of items matching the search result, or all items if search is blank/profile does has hide search mode disabled</returns>
            public List<Item> SearchResults(string search)
            {
                UpdateItems(); //Why is this here? Because the server sends the container before it sends the data with it so sometimes we get empty containers without reloading the contents
                if (search != "")
                {
                    if (ProfileManager.CurrentProfile.GridContainerSearchMode == 0) //Hide search mode
                    {
                        List<Item> filteredContents = new List<Item>();
                        foreach (Item i in containerContents)
                        {
                            if (SearchItemNameAndProps(search, i))
                                filteredContents.Add(i);
                        }
                        return filteredContents;
                    }
                }
                return containerContents;
            }

            private bool SearchItemNameAndProps(string search, Item item)
            {
                if (item == null)
                    return false;

                if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
                {
                    if (name != null && name.ToLower().Contains(search.ToLower()))
                        return true;
                    if (data != null)
                        if (data.ToLower().Contains(search.ToLower()))
                            return true;
                }
                else
                {
                    if (item.Name != null && item.Name.ToLower().Contains(search.ToLower()))
                        return true;

                    if (item.ItemData.Name.ToLower().Contains(search.ToLower()))
                        return true;
                }

                return false;
            }

            public void UpdateItems()
            {
                containerContents = GetItemsInContainer(container);
            }

            public static List<Item> GetItemsInContainer(Item _container)
            {
                List<Item> contents = new List<Item>();
                for (LinkedObject i = _container.Items; i != null; i = i.Next)
                {
                    Item item = (Item)i;
                    var layer = (Layer)item.ItemData.Layer;

                    if (_container.IsCorpse && item.Layer > 0 && !Constants.BAD_CONTAINER_LAYERS[(int)layer])
                    {
                        continue;
                    }
                    if (item.ItemData.IsWearable && (layer == Layer.Face || layer == Layer.Beard || layer == Layer.Hair))
                    {
                        continue;
                    }

                    contents.Add(item);
                }
                return contents.OrderBy((x) => x.Graphic).ToList();
            }

            public int hcount = 0;

            public void ApplyHighlightProperties()
            {
                if (ProfileManager.CurrentProfile.GridHighlight_CorpseOnly && !container.IsCorpse)
                    return;
                hcount++;
                Task.Factory.StartNew(() =>
                {
                    var tcount = hcount;
                    System.Threading.Thread.Sleep(1000);

                    if (tcount != hcount) { return; } //Another call has already been made
                    List<GridHighlightData> highlightConfigs = new List<GridHighlightData>();
                    for (int propIndex = 0; propIndex < ProfileManager.CurrentProfile.GridHighlight_PropNames.Count; propIndex++)
                    {
                        highlightConfigs.Add(GridHighlightData.GetGridHighlightData(propIndex));
                    }

                    foreach (var item in gridSlots) //For each grid slot
                    {
                        item.Value.SetHighLightBorder(0);
                        if (item.Value.SlotItem != null)
                            foreach (GridHighlightData configData in highlightConfigs) //For each highlight configuration
                            {
                                bool fullMatch = true;
                                for (int i = 0; i < configData.Properties.Count; i++) //For each property in the highlight config
                                {
                                    if (!fullMatch)
                                        break;
                                    string propText = configData.Properties[i];

                                    if (World.OPL.TryGetNameAndData(item.Value.SlotItem.Serial, out string name, out string data))
                                    {
                                        if (data != null)
                                        {
                                            string[] lines = data.Split(new string[] { "\n", "<br>" }, StringSplitOptions.None);
                                            bool hasProp = false;
                                            foreach (string line in lines) //For each property on the item
                                            {
                                                if (line.ToLower().Contains(propText.ToLower()))
                                                {
                                                    hasProp = true;
                                                    Match m = Regex.Match(line, @"\d+");
                                                    if (m.Success) //There is a number
                                                    {
                                                        if (int.TryParse(m.Value, out int val))
                                                        {
                                                            if (val >= configData.PropMinVal[i])
                                                                fullMatch = true;
                                                            else
                                                                fullMatch = false;
                                                        }
                                                    }
                                                    else if (configData.PropMinVal[i] == -1)
                                                    {
                                                        fullMatch = true;
                                                    }
                                                    else
                                                    {
                                                        fullMatch = false;
                                                    }
                                                }
                                            }
                                            if (!hasProp) { fullMatch = false; break; }
                                        }
                                        else fullMatch = false; //No OPL data(props)
                                    }
                                    else fullMatch = false; //No OPL data(name/props)
                                }
                                if (fullMatch)
                                {
                                    item.Value.SetHighLightBorder(configData.Hue);
                                }
                            }
                    }
                });
            }

        }

        private class GridScrollArea : Control
        {
            private readonly ScrollBarBase _scrollBar;
            private int _lastWidth;
            private int _lastHeight;

            public GridScrollArea
            (
                int x,
                int y,
                int w,
                int h,
                int scroll_max_height = -1
            )
            {
                X = x;
                Y = y;
                Width = w;
                Height = h;
                _lastWidth = w;
                _lastHeight = h;

                _scrollBar = new ScrollBar(Width - 14, 0, Height);


                ScrollMaxHeight = scroll_max_height;

                _scrollBar.MinValue = 0;
                _scrollBar.MaxValue = scroll_max_height >= 0 ? scroll_max_height : Height;
                _scrollBar.Parent = this;

                AcceptMouseInput = true;
                WantUpdateSize = false;
                CanMove = true;
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways;
            }


            public int ScrollMaxHeight { get; set; } = -1;
            public ScrollbarBehaviour ScrollbarBehaviour { get; set; }
            public int ScrollValue => _scrollBar.Value;
            public int ScrollMinValue => _scrollBar.MinValue;
            public int ScrollMaxValue => _scrollBar.MaxValue;

            public Rectangle ScissorRectangle;

            public override void Update()
            {
                base.Update();

                CalculateScrollBarMaxValue();

                if (Width != _lastWidth || Height != _lastHeight)
                {
                    _scrollBar.X = Width - 14;
                    _scrollBar.Height = Height;
                    _lastWidth = Width;
                    _lastHeight = Height;
                }

                if (ScrollbarBehaviour == ScrollbarBehaviour.ShowAlways)
                {
                    _scrollBar.IsVisible = true;
                }
                else if (ScrollbarBehaviour == ScrollbarBehaviour.ShowWhenDataExceedFromView)
                {
                    _scrollBar.IsVisible = _scrollBar.MaxValue > _scrollBar.MinValue;
                }
            }

            public void Scroll(bool isup)
            {
                if (isup)
                {
                    _scrollBar.Value -= _scrollBar.ScrollStep;
                }
                else
                {
                    _scrollBar.Value += _scrollBar.ScrollStep;
                }
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                _scrollBar.Draw(batcher, x + _scrollBar.X, y + _scrollBar.Y);

                if (batcher.ClipBegin(x + ScissorRectangle.X, y + ScissorRectangle.Y, Width - 14 + ScissorRectangle.Width, Height + ScissorRectangle.Height))
                {
                    for (int i = 1; i < Children.Count; i++)
                    {
                        Control child = Children[i];

                        if (!child.IsVisible)
                        {
                            continue;
                        }

                        int finalY = y + child.Y - _scrollBar.Value + ScissorRectangle.Y;

                        child.Draw(batcher, x + child.X, finalY);
                    }

                    batcher.ClipEnd();
                }

                return true;
            }

            protected override void OnMouseWheel(MouseEventType delta)
            {
                switch (delta)
                {
                    case MouseEventType.WheelScrollUp:
                        _scrollBar.Value -= _scrollBar.ScrollStep;

                        break;

                    case MouseEventType.WheelScrollDown:
                        _scrollBar.Value += _scrollBar.ScrollStep;

                        break;
                }
            }

            public override void Clear()
            {
                for (int i = 1; i < Children.Count; i++)
                {
                    Children[i].Dispose();
                }
            }

            private void CalculateScrollBarMaxValue()
            {
                _scrollBar.Height = ScrollMaxHeight >= 0 ? ScrollMaxHeight : Height;
                bool maxValue = _scrollBar.Value == _scrollBar.MaxValue && _scrollBar.MaxValue != 0;

                int startX = 0, startY = 0, endX = 0, endY = 0;

                for (int i = 1; i < Children.Count; i++)
                {
                    Control c = Children[i];

                    if (c.IsVisible && !c.IsDisposed)
                    {
                        if (c.X < startX)
                        {
                            startX = c.X;
                        }

                        if (c.Y < startY)
                        {
                            startY = c.Y;
                        }

                        if (c.Bounds.Right > endX)
                        {
                            endX = c.Bounds.Right;
                        }

                        if (c.Bounds.Bottom > endY)
                        {
                            endY = c.Bounds.Bottom;
                        }
                    }
                }

                int width = Math.Abs(startX) + Math.Abs(endX);
                int height = Math.Abs(startY) + Math.Abs(endY) - _scrollBar.Height;
                height = Math.Max(0, height - (-ScissorRectangle.Y + ScissorRectangle.Height));

                if (height > 0)
                {
                    _scrollBar.MaxValue = height;

                    if (maxValue)
                    {
                        _scrollBar.Value = _scrollBar.MaxValue;
                    }
                }
                else
                {
                    _scrollBar.Value = _scrollBar.MaxValue = 0;
                }

                _scrollBar.UpdateOffset(0, Offset.Y);

                for (int i = 1; i < Children.Count; i++)
                {
                    Children[i].UpdateOffset(0, -_scrollBar.Value + ScissorRectangle.Y);
                }
            }
        }

        private class GridContainerPreview : Gump
        {
            private readonly AlphaBlendControl _background;
            private readonly Item _container;

            private const int WIDTH = 170;
            private const int HEIGHT = 150;
            private const int GRIDSIZE = 50;

            public GridContainerPreview(uint serial, int x, int y) : base(serial, 0)
            {
                _container = World.Items.Get(serial);
                if (_container == null)
                {
                    Dispose();
                    return;
                }

                X = x - WIDTH - 20;
                Y = y - HEIGHT - 20;
                _background = new AlphaBlendControl();
                _background.Width = WIDTH;
                _background.Height = HEIGHT;

                CanCloseWithRightClick = true;
                Add(_background);
                InvalidateContents = true;
            }

            protected override void UpdateContents()
            {
                base.UpdateContents();
                if (InvalidateContents && !IsDisposed && IsVisible)
                {
                    if (_container != null && _container.Items != null)
                    {
                        int currentCount = 0, lastX = 0, lastY = 0;
                        for (LinkedObject i = _container.Items; i != null; i = i.Next)
                        {

                            Item item = (Item)i;
                            if (item == null)
                                continue;

                            if (currentCount > 8)
                                break;

                            StaticPic gridItem = new StaticPic(item.DisplayedGraphic, item.Hue);
                            gridItem.X = lastX;
                            if (gridItem.X + GRIDSIZE > WIDTH)
                            {
                                gridItem.X = 0;
                                lastX = 0;
                                lastY += GRIDSIZE;

                            }
                            lastX += GRIDSIZE;
                            gridItem.Y = lastY;
                            //gridItem.Width = GRIDSIZE;
                            //gridItem.Height = GRIDSIZE;
                            Add(gridItem);

                            currentCount++;


                        }
                    }
                }
            }

            public override void Update()
            {
                if (_container == null || _container.IsDestroyed || _container.OnGround && _container.Distance > 3)
                {
                    Dispose();

                    return;
                }

                base.Update();

                if (IsDisposed)
                {
                    return;
                }

                base.Update();
            }
        }

        private class GridSaveSystem
        {
            /// <summary>
            /// Time cutoff in seconds
            /// 60*60 = 1 hour
            ///      * 24 = 1 day
            ///          * 60 = ~2 month
            /// </summary>
            private const long TIME_CUTOFF = ((60 * 60) * 24) * 60;
            private string gridSavePath = Path.Combine(ProfileManager.ProfilePath, "GridContainers.xml");
            private XDocument saveDocument;
            private XElement rootElement;
            private bool enabled = false;

            private static GridSaveSystem instance;
            public static GridSaveSystem Instance
            {
                get
                {
                    if (instance == null)
                        instance = new GridSaveSystem();
                    return instance;
                }
            }

            private GridSaveSystem()
            {
                if (!SaveFileCheck())
                {
                    enabled = false;
                    return;
                }

                try
                {
                    saveDocument = XDocument.Load(gridSavePath);
                }
                catch
                {
                    saveDocument = new XDocument();
                }

                rootElement = saveDocument.Element("grid_gumps");
                if (rootElement == null)
                {
                    saveDocument.Add(new XElement("grid_gumps"));
                    rootElement = saveDocument.Root;
                }
                enabled = true;
            }

            public bool SaveContainer(uint serial, Dictionary<int, GridItem> gridSlots, int width, int height, int lastX = 100, int lastY = 100, bool? useOriginalContainer = false)
            {
                if (!enabled)
                    return false;

                if (useOriginalContainer == null)
                    useOriginalContainer = false;

                XElement thisContainer = rootElement.Element("container_" + serial.ToString());
                if (thisContainer == null)
                {
                    thisContainer = new XElement("container_" + serial.ToString());
                    rootElement.Add(thisContainer);
                }
                else
                    thisContainer.RemoveNodes();

                thisContainer.SetAttributeValue("last_opened", DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
                thisContainer.SetAttributeValue("width", width.ToString());
                thisContainer.SetAttributeValue("height", height.ToString());
                thisContainer.SetAttributeValue("lastX", lastX.ToString());
                thisContainer.SetAttributeValue("lastY", lastY.ToString());
                thisContainer.SetAttributeValue("useOriginalContainer", useOriginalContainer.ToString());

                foreach (var slot in gridSlots)
                {
                    if (slot.Value.SlotItem == null)
                        continue;
                    XElement item_slot = new XElement("item");
                    item_slot.SetAttributeValue("serial", slot.Value.SlotItem.Serial.ToString());
                    item_slot.SetAttributeValue("locked", slot.Value.ItemGridLocked.ToString());
                    item_slot.SetAttributeValue("slot", slot.Key.ToString());
                    thisContainer.Add(item_slot);
                }
                RemoveOldContainers();

                saveDocument.Save(gridSavePath);

                return true;
            }

            public List<GridItemSlotSaveData> GetItemSlots(uint container)
            {
                List<GridItemSlotSaveData> items = new List<GridItemSlotSaveData>();

                XElement thisContainer = rootElement.Element("container_" + container.ToString());
                if (thisContainer != null)
                {
                    foreach (XElement itemSlot in thisContainer.Elements("item"))
                    {
                        XAttribute slot, serial, isLockedAttribute;
                        slot = itemSlot.Attribute("slot");
                        serial = itemSlot.Attribute("serial");
                        isLockedAttribute = itemSlot.Attribute("locked");
                        if (slot != null && serial != null)
                        {
                            if (int.TryParse(slot.Value, out int slotV))
                                if (uint.TryParse(serial.Value, out uint serialV))
                                {
                                    if (isLockedAttribute != null && bool.TryParse(isLockedAttribute.Value, out bool isLocked))
                                        items.Add(new GridItemSlotSaveData(slotV, serialV, isLocked));
                                    else
                                        items.Add(new GridItemSlotSaveData(slotV, serialV, false));
                                }
                        }
                    }
                }

                return items;
            }

            public class GridItemSlotSaveData
            {
                public readonly int Slot;
                public readonly uint Serial;
                public readonly bool IsLocked;

                public GridItemSlotSaveData(int slot, uint serial, bool isLocked)
                {
                    this.Slot = slot;
                    this.Serial = serial;
                    this.IsLocked = isLocked;
                }
            }

            public Point GetLastSize(uint container)
            {
                Point lastSize = new Point(GridContainer.DEFAULT_WIDTH, GridContainer.DEFAULT_HEIGHT);

                XElement thisContainer = rootElement.Element("container_" + container.ToString());
                if (thisContainer != null)
                {
                    XAttribute width, height;
                    width = thisContainer.Attribute("width");
                    height = thisContainer.Attribute("height");
                    if (width != null && height != null)
                    {
                        int.TryParse(width.Value, out lastSize.X);
                        int.TryParse(height.Value, out lastSize.Y);
                    }
                }

                return lastSize;
            }

            public Point GetLastPosition(uint container)
            {
                Point LastPos = new Point(GridContainer._lastX, GridContainer._lastY);

                XElement thisContainer = rootElement.Element("container_" + container.ToString());
                if (thisContainer != null)
                {
                    XAttribute lastX, lastY;
                    lastX = thisContainer.Attribute("lastX");
                    lastY = thisContainer.Attribute("lastY");
                    if (lastX != null && lastY != null)
                    {
                        int.TryParse(lastX.Value, out LastPos.X);
                        int.TryParse(lastY.Value, out LastPos.Y);
                    }
                }

                return LastPos;
            }

            public bool UseOriginalContainerGump(uint container)
            {
                bool useOriginalContainer = false;

                XElement thisContainer = rootElement.Element("container_" + container.ToString());
                if (thisContainer != null)
                {
                    XAttribute useOriginal;
                    useOriginal = thisContainer.Attribute("useOriginalContainer");
                    if (useOriginal != null)
                    {
                        bool.TryParse(useOriginal.Value, out useOriginalContainer);
                    }
                }

                return useOriginalContainer;
            }

            private void RemoveOldContainers()
            {
                long cutOffTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - TIME_CUTOFF;
                List<XElement> removeMe = new List<XElement>();
                foreach (XElement container in rootElement.Elements())
                {
                    XAttribute lastOpened = container.Attribute("last_opened");
                    if (lastOpened != null)
                    {
                        long lo = cutOffTime;
                        long.TryParse(lastOpened.Value, out lo);

                        if (lo < cutOffTime)
                            removeMe.Add(container);
                    }
                }
                foreach (XElement container in removeMe)
                    container.Remove();
            }

            private bool SaveFileCheck()
            {
                try
                {
                    if (!File.Exists(gridSavePath))
                        File.Create(gridSavePath).Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not create file: " + gridSavePath);
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.AppendLine("######################## [START LOG] ########################");

                    sb.AppendLine($"TazUO - {CUOEnviroment.Version} - {DateTime.Now}");

                    sb.AppendLine
                        ($"OS: {Environment.OSVersion.Platform} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");

                    sb.AppendLine();

                    if (Settings.GlobalSettings != null)
                    {
                        sb.AppendLine($"Shard: {Settings.GlobalSettings.IP}");
                        sb.AppendLine($"ClientVersion: {Settings.GlobalSettings.ClientVersion}");
                        sb.AppendLine();
                    }

                    sb.AppendFormat("Exception:\n{0}\n", e);
                    sb.AppendLine("######################## [END LOG] ########################");
                    sb.AppendLine();
                    sb.AppendLine();

                    Log.Panic(e.ToString());
                    string path = Path.Combine(CUOEnviroment.ExecutablePath, "Logs");

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    using (LogFile crashfile = new LogFile(path, "crash.txt"))
                    {
                        crashfile.WriteAsync(sb.ToString()).RunSynchronously();
                    }
                    return false;
                }
                return true;
            }

            public void Clear()
            {
                instance = null;
            }
        }
    }
}