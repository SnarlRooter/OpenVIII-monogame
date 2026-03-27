using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenVIII
{
    namespace Kernel
    {
        /// <summary>
        /// Attack Flags effects how the attack can be treated.
        /// </summary>
        /// <see cref="https://github.com/alexfilth/doomtrain/blob/master/Doomtrain/MainForm.cs"/>
        [Flags]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public enum AttackFlags
        {
            None = 0x0,
            Shelled = 0x1,
            Unk0X2 = 0x2,
            Unk0X4 = 0x4,
            BreakDamageLimit = 0x8,
            Reflected = 0x10,
            Unk0X20 = 0x20,
            Unk0X40 = 0x40,
            Revive = 0x80,
            Attacker = 0x100
        }

        public static class AttackFlagsExtensions
        {
            private static Saves.CharacterData _attackerData;
            private static Damageable _attackerDamageable;

            public static void SetAttackerData(this AttackFlags flags, Saves.CharacterData attacker)
            {
                if ((flags & AttackFlags.Attacker) != 0)
                {
                    _attackerData = attacker;
                    _attackerDamageable = attacker; // CharacterData is also a Damageable
                }
            }

            public static void SetAttackerData(this AttackFlags flags, Damageable attacker)
            {
                if ((flags & AttackFlags.Attacker) != 0)
                {
                    _attackerDamageable = attacker;
                }
            }

            public static bool GetAttackerData(this AttackFlags flags, out Saves.CharacterData attacker)
            {
                if ((flags & AttackFlags.Attacker) != 0)
                {
                    attacker = _attackerData;
                    return true;
                }
                attacker = null;
                return false;
            }

            public static bool GetAttackerData(this AttackFlags flags, out Damageable attacker)
            {
                if ((flags & AttackFlags.Attacker) != 0)
                {
                    // Check _attackerDamageable first, then fall back to _attackerData
                    attacker = _attackerDamageable ?? _attackerData;
                    return attacker != null;
                }
                attacker = null;
                return false;
            }
        }
    }
}