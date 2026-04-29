using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        /// The looping EXP sound.
        /// </summary>
        private Audio ExpSound;

        private Box header;

        /// <summary>
        /// Time elapsed in current countdown cycle.
        /// </summary>
        private double TimeRemaining;

        /// <summary>
        /// Visual copy of ExtraExp for the countdown animation.
        /// </summary>
        private ConcurrentDictionary<Characters, int> _visualExtraExp;

        #endregion Fields

        #region Destructors
        ~PlayerEXP()
        {
            Dispose(false);
        }
        #endregion Destructors

        #region Properties
        /// <summary>
        /// Display EXP for countdown (what's shown on screen).
        /// </summary>
        public int DisplayExp
        {
            get => _battleExpPool + (ExtraExp?.Values.Sum() ?? 0); 
            set
            {
                _battleExpPool = Math.Max(0, value - (ExtraExp?.Values.Sum() ?? 0));
                RefreshDisplay();
            }
        }

        public ConcurrentDictionary<Characters, int> ExtraExp
        {
            get => _extraExp;
            set
            {
                _extraExp = value;
                RefreshDisplay();
            }
        }

        private ConcurrentDictionary<Characters, int> _extraExp;
        public bool NoEarnExp { get; internal set; } = false;

        private bool HasRemainingExp => _battleExpPool > 0 || (ExtraExp != null && ExtraExp.Count > 0);
        private bool IsAnimating => _battleExpPool > 0 || (_visualExtraExp != null && _visualExtraExp.Values.Any(v => v > 0));
        #endregion Properties

        #region Methods
        public static new PlayerEXP Create(params Menu_Base[] d) => Create<PlayerEXP>(d);

        public void Dispose()
        {
            Dispose(true);
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
                _totalExpToDistribute = _battleExpPool;
                if (ExpSound == null)
                    ExpSound = Sound.Play(34, loop: true);

                if (ExtraExp != null)
                {
                    _visualExtraExp = new ConcurrentDictionary<Characters, int>(ExtraExp);
                }
                return true;
            }

            if (_isCountingDown)
            {
                var totalExp = _totalExpToDistribute;
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
                        var expPerChar = totalExp / partyCount;
                        if (ExtraExp != null && ExtraExp.TryGetValue(c.ID, out var bonus))
                            expPerChar += bonus;
                        c.Experience += (uint)expPerChar;
                    }
                }

                _totalExpToDistribute = 0;
                _battleExpPool = 0;
                ExtraExp = null;
                _visualExtraExp = null;
                _isCountingDown = false;

                if (ExpSound != null)
                {
                    ExpSound.Stop();
                    ExpSound = null;
                }

                Refresh();
                return true;
            }
            return false;
        }

        public override bool Update()
        {
            if (_isCountingDown)
            {
                TimeRemaining += Memory.ElapsedGameTime.TotalMilliseconds / ExpDistributionSpeed;
                if (TimeRemaining >= 1.0)
                {
                    int delta = (int)Math.Floor(TimeRemaining);
                    TimeRemaining -= delta;

                    // Update the visual base exp pool
                    if (_battleExpPool > 0)
                    {
                        int actualDelta = Math.Min(_battleExpPool, delta);
                        _battleExpPool -= actualDelta;
                    }

                    // Update the visual extra exp pool for animation
                    if (_visualExtraExp != null)
                    {
                        foreach (var key in _visualExtraExp.Keys.ToList())
                        {
                            if (_visualExtraExp.TryGetValue(key, out int val) && val > 0)
                            {
                                int actualExtraDelta = Math.Min(val, delta);
                                _visualExtraExp[key] -= actualExtraDelta;
                            }
                        }
                    }

                    RefreshDisplay();

                    if (!IsAnimating)
                    {
                        DistributeRemainingExp();
                        _isCountingDown = false;
                        if (ExpSound != null)
                        {
                            ExpSound.Stop();
                            ExpSound = null;
                        }
                    }
                }
            }
            return base.Update();
        }

        private void DistributeRemainingExp()
        {
            var partyCount = 0;
            foreach (var i in ITEM)
            {
                if (i?.Damageable != null && i.Damageable.GetCharacterData(out _))
                    partyCount++;
            }
            if (partyCount <= 0) partyCount = 1;

            if (_totalExpToDistribute > 0)
            {
                foreach (var i in ITEM)
                {
                    if (i?.Damageable == null) continue;
                    if (i.Damageable.GetCharacterData(out var c))
                    {
                        c.Experience += (uint)(_totalExpToDistribute / partyCount);
                    }
                }
            }

            if (ExtraExp != null)
            {
                foreach (var i in ITEM)
                {
                    if (i?.Damageable == null) continue;
                    if (i.Damageable.GetCharacterData(out var c))
                    {
                        if (ExtraExp.TryGetValue(c.ID, out int bonus) && bonus > 0)
                        {
                            c.Experience += (uint)bonus;
                        }
                    }
                }
            }

            _totalExpToDistribute = 0;
            _battleExpPool = 0;
            ExtraExp = null;
            _visualExtraExp = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DistributeRemainingExp();
                }
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
                if (i.Damageable.GetCharacterData(out var c))
                {
                    var tmpexp = (int)(_battleExpPool / partyCount);
                    ((IGMData.PlayerExp)i).NoEarnExp = NoEarnExp;
                    var extraDict = _visualExtraExp != null ? _visualExtraExp : ExtraExp;
                    ((IGMData.PlayerExp)i).BattleExp = tmpexp + (extraDict != null && extraDict.TryGetValue(c.ID, out int bonus) ? bonus : 0);
                }
            }
            header.Width = Width;
        }
        #endregion Methods
    }
}
