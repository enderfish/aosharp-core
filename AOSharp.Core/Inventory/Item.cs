using AOSharp.Common.GameData;
using AOSharp.Core.Combat;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AOSharp.Common.Unmanaged.Interfaces;

namespace AOSharp.Core.Inventory
{
    public class Item : ACGItem, ICombatAction, IEquatable<Item>
    {
        private const float USE_TIMEOUT = 1;

        public readonly Identity UniqueIdentity;
        public readonly Identity Slot;
        public readonly int Charges;
        public List<EquipSlot> EquipSlots => GetEquipSlots();

        public bool IsEquipped => Slot.Type != IdentityType.None && Enum.IsDefined(typeof(EquipSlot), (int)Slot.Instance);

        public static EventHandler<ItemUsedEventArgs> ItemUsed;

        public static bool HasPendingUse => _pendingUse.Slot != Identity.None;
        private static (Identity Slot, double Timeout) _pendingUse = (Identity.None, 0);

        internal Item(int lowId, int highId, int ql) : base(lowId, highId, ql)
        {
            UniqueIdentity = Identity.None;
            Slot = Identity.None;
        }

        internal Item(int lowId, int highId, int ql, int charges, Identity uniqueIdentity, Identity slot) : base(lowId, highId, ql)
        {
            UniqueIdentity = uniqueIdentity;
            Slot = slot;
            Charges = charges;
        }

        public void Equip(EquipSlot equipSlot)
        {
            MoveToInventory((int)equipSlot);
        }

        public void Use(SimpleChar target = null, bool setTarget = false)
        {
            if (target == null)
                target = DynelManager.LocalPlayer;

            if (setTarget)
                target.Target();

            Network.Send(new GenericCmdMessage()
            {
                Action = GenericCmdAction.Use,
                User = DynelManager.LocalPlayer.Identity,
                Target = Slot
            });

            _pendingUse = (Slot, Time.NormalTime + USE_TIMEOUT);
        }

        public void UseOn(Identity target)
        {
            if (target.Type == IdentityType.SimpleChar)
            {
                N3EngineClientAnarchy.UseItemOnCharacter(Slot, target);
            }
            else
            {
                N3EngineClientAnarchy.UseItemOnItem(Slot, target);
            }
        }

        public void MoveToInventory(int targetSlot = 0x6F)
        {
            MoveItemToInventory(Slot, targetSlot);
        }

        public void MoveToContainer(Container target)
        {
            ContainerAddItem(Slot, target.Identity);
        }

        public void MoveToContainer(Identity target)
        {
            ContainerAddItem(Slot, target);
        }

        public void Split(int count)
        {
            Network.Send(new CharacterActionMessage()
            {
                Action = CharacterActionType.SplitItem,
                Target = Slot,
                Parameter2 = count
            });
        }

        public void CombineWith(Identity target)
        {
            Network.Send(new CharacterActionMessage()
            {
                Action = CharacterActionType.UseItemOnItem,

                Target = Slot,
                Parameter1 = (int)target.Type,
                Parameter2 = target.Instance
            });
        }

        public void CombineWith(Item target)
        {
            CombineWith(target.Slot);
        }

        public void Drop(Vector3 position)
        {
            Network.Send(new DropTemplateMessage()
            {
                Item = Slot,
                Position = position
            });
        }

        public void Delete()
        {
            Delete(Slot);
        }

        internal static void Update()
        {
            try
            {
                if (_pendingUse.Slot != Identity.None && _pendingUse.Timeout <= Time.NormalTime)
                    _pendingUse.Slot = Identity.None;
            }
            catch (Exception e) 
            {
                Chat.WriteLine($"This shouldn't happen pls report (Item): {e.Message}");
            }
        }

        //What a meme
        internal List<EquipSlot> GetEquipSlots()
        {
            List<EquipSlot> equipSlots = new List<EquipSlot>();
            ItemClass itemClass = (ItemClass)GetStat(Stat.ItemClass);

            if (itemClass == ItemClass.None)
                return equipSlots;

            int slotStat = GetStat(Stat.Slot, 3);

            string itemClassName;

            if (itemClass == ItemClass.Weapon)
                itemClassName = "Weap";
            else if (itemClass == ItemClass.Armor)
                itemClassName = "Cloth";
            else if (itemClass == ItemClass.Implant)
                itemClassName = "Imp";
            else
                return equipSlots;

            foreach (EquipSlot equipSlot in Enum.GetValues(typeof(EquipSlot)))
            {
                if (equipSlot == EquipSlot.Social_Neck)
                    break;

                if (!equipSlot.ToString().StartsWith(itemClassName))
                    continue;

                int relativeSlotFlag = (1 << ((int)equipSlot - ((int)itemClass - 1) * 0x10));

                if ((slotStat & relativeSlotFlag) == relativeSlotFlag)
                    equipSlots.Add(equipSlot);
            }

            return equipSlots;
        }

        internal static void OnUsingItem(Identity slot)
        {
            if (slot != _pendingUse.Slot || !Inventory.Find(slot, out Item item))
                return;

            double nextTimeout = Time.NormalTime + item.AttackDelay + USE_TIMEOUT;
            _pendingUse = (slot, nextTimeout);

            if (CombatHandler.Instance != null)
                CombatHandler.Instance.OnUsingItem(item, nextTimeout);
        }

        internal static void OnItemUsed(int lowId, int highId, int ql, Identity owner)
        {
            ItemUsed?.Invoke(null, new ItemUsedEventArgs
            {
                OwnerIdentity = owner,
                Owner = DynelManager.GetDynel(owner)?.Cast<SimpleChar>(),
                Item = new Item(lowId, highId, ql)
            });

            if (owner != DynelManager.LocalPlayer.Identity)
                    return;

            _pendingUse = (Identity.None, 0);

            CombatHandler.Instance?.OnItemUsed(lowId, highId, ql);
        }

        public static void MoveItemToInventory(Identity source, int slot = 0x6F)
        {
            Network.Send(new ClientMoveItemToInventory()
            {
                SourceContainer = source,
                Slot = slot
            });
        }

        public static void ContainerAddItem(Identity source, Identity target)
        {
            Network.Send(new ClientContainerAddItem()
            {
                Source = source,
                Target = target
            });
        }

        public static void SplitItem(Identity source, int count)
        {
            Network.Send(new CharacterActionMessage()
            {
                Action = CharacterActionType.SplitItem,
                Target = source,
                Parameter2 = count
            });
        }

        public static void Use(Identity slot)
        {
            Network.Send(new GenericCmdMessage()
            {
                Action = GenericCmdAction.Use,
                User = DynelManager.LocalPlayer.Identity,
                Target = slot
            });
        }

        public static void UseItemOnItem(Identity slot, Identity target)
        {
            Network.Send(new GenericCmdMessage()
            {
                Action = GenericCmdAction.UseItemOnItem,
                User = DynelManager.LocalPlayer.Identity,
                Source = slot,
                Target = target
            });
        }

        public static void Delete(Identity slot)
        {
            Network.Send(new CharacterActionMessage()
            {
                Action = CharacterActionType.DeleteItem,
                Target = slot,
            });
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Item);
        }

        public bool Equals(Item other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return Id == other.Id && HighId == other.HighId && QualityLevel == other.QualityLevel && Slot == other.Slot;
        }

        public override int GetHashCode()
        {
            int hashCode = 1039555169;
            hashCode = hashCode * -1521134295 + Id.GetHashCode();
            hashCode = hashCode * -1521134295 + HighId.GetHashCode();
            hashCode = hashCode * -1521134295 + QualityLevel.GetHashCode();
            hashCode = hashCode * -1521134295 + Slot.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Item a, Item b)
        {
            if (object.ReferenceEquals(a, null))
                return object.ReferenceEquals(b, null);

            return a.Equals(b);
        }

        public static bool operator !=(Item a, Item b) => !(a == b);
    }

    public class ItemUsedEventArgs : EventArgs
    {
        public SimpleChar Owner { get; set; }
        public Identity OwnerIdentity { get; set; }
        public Item Item { get; set; }
    }
}
