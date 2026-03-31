using Microsoft.Xna.Framework;
using OpenVIII.Kernel;
using static OpenVIII.IGM;
using System.Collections.Generic;
using System.Linq;

namespace OpenVIII.IGMData
{
    public class GF : IGMData.Base
    {
        #region Fields

        private IGMDataItem.Box _headerBox;
        private IGMDataItem.Box _abilitiesBox;
        private bool _showingAbilities;
        private GFs _currentGF;
        private Saves.Data _source;

        #endregion Fields

        #region Properties

        public Dictionary<GFs, Characters> JunctionedGFs { get; private set; }
        public IEnumerable<GFs> UnlockedGFs { get; private set; }
        public GFs[] Contents { get; set; }
        public int Page { get; protected set; }
        public int Pages { get; private set; }
        public int DefaultPages { get; set; } = 2;
        public Saves.Data Source { get => _source; private set => _source = value; }

        #endregion Properties

        #region Methods

        public static GF Create(Rectangle? pos = null)
        {
            var r = IGMData.Base.Create<GF>(10, 5, new IGMDataItem.Box
            {
                Pos = pos ?? new Rectangle(125, 69, 695, 552),
                Title = Icons.ID.GF
            }, 1, 8);
            r.DefaultPages = 2;
            r.ResetPages();
            r.Init();
            r.Refresh();
            return r;
        }

        public void ResetPages()
        {
            Pages = DefaultPages;
            if (Pages <= 1)
            {
                Cursor_Status |= Cursor_Status.Horizontal;
            }
            else
            {
                Cursor_Status &= ~Cursor_Status.Horizontal;
            }
        }

        public override bool Inputs_CANCEL()
        {
            if (_showingAbilities)
            {
                HideAbilitiesPanel();
                return true;
            }
            Hide();
            Menu.IGM.SetMode(IGM.Mode.ChooseItem);
            return true;
        }

        public override bool Inputs_OKAY()
        {
            if (_showingAbilities)
            {
                TrySelectAbility();
                return true;
            }

            base.Inputs_OKAY();
            var select = Contents[CURSOR_SELECT];

            if (select == GFs.Blank)
                return false;

            ShowAbilitiesPanel(select);
            return true;
        }

        private void ShowAbilitiesPanel(GFs g)
        {
            _showingAbilities = true;
            _currentGF = g;
            
            for (var i = 0; i < Rows; i++)
            {
                ITEM[i, 0].Hide();
                ITEM[i, 1].Hide();
                ITEM[i, 2].Hide();
            }
            
            _abilitiesBox.Show();
            for (var i = 0; i < 8; i++)
            {
                ITEM[i, 3].Show();
                ITEM[i, 4].Show();
            }
            
            RefreshAbilitiesPanel(g);
            CURSOR_SELECT = 0;
            Cursor_Status |= Cursor_Status.Enabled;
            base.SetCursor_select(0);
        }

        private void HideAbilitiesPanel()
        {
            _showingAbilities = false;
            _currentGF = GFs.Blank;
            _abilitiesBox.Hide();
            
            for (var i = 0; i < 8; i++)
            {
                ITEM[i, 3].Hide();
                ITEM[i, 4].Hide();
            }
            
            Refresh();
        }

        private void RefreshAbilitiesPanel(GFs g)
        {
            if (g == GFs.Blank)
                return;

            var gfName = Memory.Strings.GetName(g);
            _headerBox.Data = gfName;

            if (Source == null || Memory.KernelBin == null || Memory.KernelBin.JunctionableGFsData == null)
                return;

            var gfData = Source[g];
            if (gfData == null)
                return;

            if (!Memory.KernelBin.JunctionableGFsData.TryGetValue(g, out var gfInfo) || gfInfo.Ability == null)
                return;

            var aps = gfData.APs;
            var learning = gfData.Learning;
            var complete = gfData.Complete;

            var abilityCount = gfInfo.Ability.Count;
            for (var i = 0; i < abilityCount && i < 8; i++)
            {
                var ability = gfInfo.Ability[i].Key;

                if (!Memory.KernelBin.AllAbilities.TryGetValue(ability, out var abilityData))
                    continue;

                var ap = aps != null && i < aps.Length ? aps[i] : (byte)0;
                var isComplete = complete != null && (int)ability < complete.Length && complete[(int)ability];
                var isLearning = learning == ability;

                var color = Font.ColorID.White;
                if (isComplete)
                    color = Font.ColorID.Grey;
                else if (isLearning)
                    color = Font.ColorID.Green;

                ((IGMDataItem.Text)ITEM[i, 3]).Data = abilityData.Name;
                ((IGMDataItem.Text)ITEM[i, 3]).FontColor = color;
                ((IGMDataItem.Integer)ITEM[i, 4]).Data = ap;

                ITEM[i, 3].Show();
                ITEM[i, 4].Show();
            }

            for (var i = abilityCount; i < 8; i++)
            {
                ITEM[i, 3].Hide();
                ITEM[i, 4].Hide();
            }
        }

        private void TrySelectAbility()
        {
            var cursor = CURSOR_SELECT;
            if (cursor < 0 || cursor >= 8)
                return;

            var select = _currentGF;
            if (select == GFs.Blank)
                return;

            var gfData = Source[select];
            if (gfData == null)
                return;

            if (Memory.KernelBin == null || Memory.KernelBin.JunctionableGFsData == null)
                return;

            if (!Memory.KernelBin.JunctionableGFsData.TryGetValue(select, out var gfInfo) || gfInfo.Ability == null)
                return;

            if (cursor >= gfInfo.Ability.Count)
                return;

            var ability = gfInfo.Ability[cursor].Key;

            if (ability != Kernel.Abilities.None && !gfData.Complete[(int)ability])
            {
                gfData.SetLearning(ability);
                AV.Sound.Play(31);
                RefreshAbilitiesPanel(select);
            }
        }

        public override void Refresh()
        {
            if (Memory.State == null) return;
            Source = Memory.State;
            JunctionedGFs = Source.JunctionedGFs();
            UnlockedGFs = Source.UnlockedGFs;

            if (_showingAbilities)
            {
                _abilitiesBox.Show();
                for (var i = 0; i < Rows; i++)
                {
                    ITEM[i, 0].Hide();
                    ITEM[i, 1].Hide();
                    ITEM[i, 2].Hide();
                }
                for (var i = 0; i < 8; i++)
                {
                    ITEM[i, 3].Show();
                    ITEM[i, 4].Show();
                }
                if (_currentGF != GFs.Blank)
                    RefreshAbilitiesPanel(_currentGF);
            }
            else
            {
                _abilitiesBox.Hide();
                for (var i = 0; i < 8; i++)
                {
                    ITEM[i, 3].Hide();
                    ITEM[i, 4].Hide();
                }
                var pos = 0;
                var skip = Page * Rows;
                foreach (var g in UnlockedGFs)
                {
                    if (pos >= Rows) break;
                    if (skip > 0)
                    {
                        skip--;
                        HideChild(pos);
                    }
                    else
                    {
                        Contents[pos] = g;
                        var gfData = Source[g];
                        var color = gfData.IsDead ? Font.ColorID.Red : Font.ColorID.White;
                        ((IGMDataItem.Face)ITEM[pos, 0]).Data = g.ToFacesID();
                        ((IGMDataItem.Text)ITEM[pos, 1]).Data = Memory.Strings.GetName(g);
                        ((IGMDataItem.Text)ITEM[pos, 1]).FontColor = color;
                        ((IGMDataItem.Integer)ITEM[pos, 2]).Data = gfData.Level;
                        ShowChild(pos);
                    }
                    pos++;
                }
                for (; pos < Rows; pos++)
                    HideChild(pos);
            }

            base.Refresh();
            UpdateTitle();
        }

        protected override void SetCursor_select(int value)
        {
            if (!value.Equals(GetCursor_select()))
            {
                base.SetCursor_select(value);
                UpdateHeaderName();
                
                if (_showingAbilities && _currentGF != GFs.Blank)
                {
                    RefreshAbilitiesPanel(_currentGF);
                }
            }
        }

        private void UpdateHeaderName()
        {
            var select = Contents[CURSOR_SELECT];
            if (select != GFs.Blank && _headerBox != null)
            {
                var gfName = Memory.Strings.GetName(select);
                _headerBox.Data = gfName;
            }
        }

        public void UpdateTitle()
        {
            if (Pages == 1)
            {
                ((IGMDataItem.Box)CONTAINER).Title = Icons.ID.GF;
                ITEM[Rows, 0].Hide();
                ITEM[Rows + 1, 0].Hide();
            }
            else
            {
                ((IGMDataItem.Box)CONTAINER).Title = Icons.ID.GF_PG1 + checked((byte)Page);
                ITEM[Rows, 0].Show();
                ITEM[Rows + 1, 0].Show();
            }
        }

        protected override void Init()
        {
            base.Init();
            Contents = new GFs[Rows];
            Cursor_Status |= (Cursor_Status.Enabled | Cursor_Status.Vertical);
            Page = 0;

            _headerBox = new IGMDataItem.Box
            {
                Pos = new Rectangle(130, 74, 305, 60),
                Title = Icons.ID.None
            };
            ITEM[Rows, 0] = _headerBox;

            _abilitiesBox = new IGMDataItem.Box
            {
                Pos = new Rectangle(440, 74, 380, 440),
                Title = Icons.ID.ABILITY
            };
            ITEM[Rows + 1, 0] = _abilitiesBox;

            InitAbilitiesPanel();

            for (var i = 0; i < Rows;)
                AddGF(ref i, GFs.Blank);
        }

        private void InitAbilitiesPanel()
        {
            for (var i = 0; i < 8; i++)
            {
                var rowY = 85 + (i * 40);
                ITEM[i, 3] = new IGMDataItem.Text
                {
                    Pos = new Rectangle(455, rowY, 260, 35),
                    Scale = new Vector2(0.8f)
                };
                ITEM[i, 4] = new IGMDataItem.Integer
                {
                    Pos = new Rectangle(720, rowY, 60, 35),
                    Spaces = 3
                };
                ITEM[i, 3].Hide();
                ITEM[i, 4].Hide();
            }
        }

        protected override void InitShift(int i, int col, int row)
        {
            base.InitShift(i, col, row);
            if (i < Rows)
            {
                var cellWidth = 400;
                var cellHeight = 60;
                SIZE[i].X = 130;
                SIZE[i].Y = 140 + (i * cellHeight);
                SIZE[i].Width = cellWidth;
                SIZE[i].Height = cellHeight;
            }
        }

        public override void Inputs_Left()
        {
            if (Pages > 1)
            {
                do
                {
                    Page--;
                    if (Page < 0)
                        Page = Pages - 1;
                    Refresh();
                }
                while (!ITEM[0, 0].Enabled && Page != 0);
            }
        }

        public override void Inputs_Right()
        {
            if (Pages > 1)
            {
                do
                {
                    Page++;
                    if (Page >= Pages)
                        Page = 0;
                    Refresh();
                }
                while (!ITEM[0, 0].Enabled && Page != 0);
            }
        }

        private void AddGF(ref int pos, GFs g)
        {
            Contents[pos] = g;
            if (g != GFs.Blank)
            {
                if (ITEM[pos, 0] == null)
                    ITEM[pos, 0] = new IGMDataItem.Face { Pos = SIZE[pos], Scale = new Vector2(1.0f) };
                if (ITEM[pos, 1] == null)
                    ITEM[pos, 1] = new IGMDataItem.Text { Pos = new Rectangle(SIZE[pos].X, SIZE[pos].Y + SIZE[pos].Height - 15, SIZE[pos].Width, 15), Scale = new Vector2(0.7f) };
                if (ITEM[pos, 2] == null)
                    ITEM[pos, 2] = new IGMDataItem.Integer { Pos = new Rectangle(SIZE[pos].X + SIZE[pos].Width - 15, SIZE[pos].Y + SIZE[pos].Height - 15, 15, 15), Spaces = 2 };
                ShowChild(pos);
            }
            else
            {
                if (ITEM[pos, 0] == null)
                    ITEM[pos, 0] = new IGMDataItem.Face { Pos = SIZE[pos], Scale = new Vector2(1.0f) };
                if (ITEM[pos, 1] == null)
                    ITEM[pos, 1] = new IGMDataItem.Text { Pos = new Rectangle(SIZE[pos].X, SIZE[pos].Y + SIZE[pos].Height - 15, SIZE[pos].Width, 15), Scale = new Vector2(0.7f) };
                if (ITEM[pos, 2] == null)
                    ITEM[pos, 2] = new IGMDataItem.Integer { Pos = new Rectangle(SIZE[pos].X + SIZE[pos].Width - 15, SIZE[pos].Y + SIZE[pos].Height - 15, 15, 15), Spaces = 2 };
                HideChild(pos);
            }
            pos++;
        }

        private void HideChild(int pos)
        {
            ITEM[pos, 0].Hide();
            ITEM[pos, 1].Hide();
            ITEM[pos, 2].Hide();
        }

        private void ShowChild(int pos)
        {
            ITEM[pos, 0].Show();
            ITEM[pos, 1].Show();
            ITEM[pos, 2].Show();
        }

        #endregion Methods
    }
}
