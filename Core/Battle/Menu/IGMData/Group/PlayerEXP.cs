using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenVIII.AV;
using OpenVIII.IGMDataItem;

namespace OpenVIII.IGMData.Group
{
    public class PlayerEXP : Base, IDisposable
    {
        #region Fields

       /// <summary>
        /// Speed of EXP distribution countdown (milliseconds per tick).
        /// Smaller value = faster countdown.
        /// </summary>
        private const float ExpDistributionSpeed = 4f;

        /// <summary>
        /// Total EXP from defeated enemies being distributed to party.
        /// </summary>
        private int _battleExpPool;

        /// <summary>
        /// Total EXP to distribute when countdown ends.
        /// </summary>
        private int _totalExpToDistribute;

        /// <summary>
        /// Are we in countdown mode distributing EXP.
        /// </summary>
        private bool _isCountingDown;

        private bool _disposedValue;

        /// <summary>
        /// The looping EXP sound. Need to track the object here to stop the loop.
        /// </summary>
        private Audio ExpSound;

        private Box header;

        /// <summary>
        /// Time elapsed in current countdown cycle.
        /// </summary>
        private double TimeRemaining;

        #endregion Fields

        #region Destructors

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~PlayerEXP()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        #endregion Destructors

        #region Properties

        /// <summary>
        /// Display EXP for countdown (what's shown on screen).
        /// </summary>
        public int DisplayExp
        {
            get => _battleExpPool; set
            {
                _battleExpPool = value;
                RefreshDisplay();
            }
        }

        public ConcurrentDictionary<Characters, int> ExtraExp { get; set; }
        public bool NoEarnExp { get; internal set; } = false;

        private bool HasRemainingExp => (_battleExpPool > 0 || ExtraExp != null && ExtraExp.Count > 0);

        #endregion Properties

        #region Methods

        public static new PlayerEXP Create(params Menu_Base[] d) => Create<PlayerEXP>(d);

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        public override void Draw()
        {
            if (Enabled)
                header?.Draw();
            base.Draw();
        }

        public override bool Inputs_CANCEL() => false;

        public override bool Inputs_OKAY()
        {
            base.Inputs_OKAY();
            if (!_isCountingDown && HasRemainingExp)
            {
                _isCountingDown = true;
                _totalExpToDistribute = DisplayExp;
                if (ExpSound == null)
                    ExpSound = Sound.Play(34, loop: true);
                return true;
            }

            if (_isCountingDown)
            {
                var totalExp = _totalExpToDistribute;

                // First, count how many characters will actually receive EXP
                var partyCount = 0;
                foreach (var i in ITEM)
                {
                    if (i?.Damageable != null && i.Damageable.GetCharacterData(out _))
                        partyCount++;
                }
                if (partyCount <= 0) partyCount = 1;

                // Now distribute EXP to each character directly in Memory.State
                foreach (var i in ITEM)
                {
                    if (i?.Damageable == null) continue;
                    if (i.Damageable.GetCharacterData(out var c))
                    {
                        var expPerChar = totalExp / partyCount;
                        if (ExtraExp != null && ExtraExp.TryGetValue(c.ID, out var bonus))
                            expPerChar += bonus;
                        // Directly update the global state - this is what PlayerExp.Update() reads!
                        c.Experience += (uint)expPerChar;
                    }
                }

                // Reset all tracking variables
                _totalExpToDistribute = 0;
                _battleExpPool = 0;
                ExtraExp = null;
                _isCountingDown = false;

                if (ExpSound != null)
                {
                    ExpSound.Stop();
                    ExpSound = null;
                }

                // Force refresh the display to show updated values from Memory.State
                Refresh();

                return true;
            }
            return false;
        }

        public override bool Update()
        {
            if (_isCountingDown)
            {
                if (HasRemainingExp)
                {
                   if ((TimeRemaining += Memory.ElapsedGameTime.TotalMilliseconds / ExpDistributionSpeed) > 1)
                        {
                            if (DisplayExp > 0)
                            {
                                DisplayExp -= (int)TimeRemaining;
                            }
                        else
                        {
                            var total = 0;
                            if (ExtraExp != null)
                            {
                                foreach (var e in ExtraExp)
                                {
                                    if (e.Value > 0)
                                        total += (ExtraExp[e.Key] -= (int)TimeRemaining);
                                    RefreshDisplay();
                                }

                                if (total <= 0)
                                    ExtraExp = null;
                            }
                        }
                       TimeRemaining -= (int)TimeRemaining;
                    }
                }
                else
                {
                    DistributeRemainingExp();
                    _isCountingDown = false;
                    ExpSound.Stop();
                    ExpSound = null;
                }
            }
            return base.Update();
        }

        private void DistributeRemainingExp()
        {
            if (_totalExpToDistribute > 0)
            {
                var partyCount = 0;
                foreach (var i in ITEM)
                {
                    if (i?.Damageable != null && i.Damageable.GetCharacterData(out _))
                        partyCount++;
                }
                if (partyCount <= 0) partyCount = 1;

                foreach (var i in ITEM)
                {
                    if (i?.Damageable == null) continue;
                    if (i.Damageable.GetCharacterData(out var c))
                    {
                        var expPerChar = _totalExpToDistribute / partyCount;
                        if (ExtraExp != null && ExtraExp.TryGetValue(c.ID, out var bonus))
                            expPerChar += bonus;
                        c.Experience += (uint)expPerChar;
                    }
                }

                _totalExpToDistribute = 0;
                _battleExpPool = 0;
                ExtraExp = null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DistributeRemainingExp();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                header.Dispose();
                _disposedValue = true;
            }
        }

      protected override void Init()
        {
            base.Init();
            Cursor_Status |= (Cursor_Status.Hidden | (Cursor_Status.Enabled | Cursor_Status.Static));
            header = new Box { Data = Strings.Name.EXP_received, Pos = new Rectangle(0, 0, CONTAINER.Width, 78), Title = Icons.ID.INFO, Options = Box_Options.Middle };
        }

     private void RefreshDisplay()
        {
            var partyCount = 0;
            foreach (var i in ITEM)
                if (i != null && i.Damageable != null)
                    partyCount++;
            if (partyCount <= 0) partyCount = 1;
            foreach (var i in ITEM)
            {
                if (i?.Damageable == null) continue;
                var tmpexp = (int)(DisplayExp / partyCount);
                ((IGMData.PlayerExp)i).NoEarnExp = NoEarnExp;
                ((IGMData.PlayerExp)i).BattleExp = tmpexp;
            }
            header.Width = Width;
        }

        #endregion Methods
    }
}