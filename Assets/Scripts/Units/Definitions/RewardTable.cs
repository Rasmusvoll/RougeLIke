using System;
using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    public enum RewardKind { Body, Part, Buff }

    [Serializable]
    public class RewardEntry
    {
        public RewardKind kind;
        public ContentDefinition content;
        public int weight = 1;
    }

    [CreateAssetMenu(menuName = "Units/Reward Table")]
    public class RewardTable : ScriptableObject
    {
        public List<RewardEntry> entries = new();
        public int offerCount = 3;

        /// <summary>
        /// Rolls up to offerCount distinct weighted offers, skipping bodies the player already owns.
        /// Pass a seeded System.Random so runs can be replayed.
        /// </summary>
        public List<RewardEntry> Roll(System.Random rng, PlayerCollection owned)
        {
            var pool = new List<RewardEntry>();
            foreach (var e in entries)
            {
                if (e == null || e.content == null || e.weight <= 0) continue;
                if (e.kind == RewardKind.Body && owned != null && owned.OwnsBody(e.content.id)) continue;
                pool.Add(e);
            }

            var offers = new List<RewardEntry>();
            while (offers.Count < offerCount && pool.Count > 0)
            {
                int total = 0;
                foreach (var e in pool) total += e.weight;
                int pick = rng.Next(total);
                for (int i = 0; i < pool.Count; i++)
                {
                    pick -= pool[i].weight;
                    if (pick < 0)
                    {
                        offers.Add(pool[i]);
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }
            return offers;
        }
    }
}
