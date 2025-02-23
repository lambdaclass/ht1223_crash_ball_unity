using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Tools;
using UnityEngine;
using Game;

public class Loot : MonoBehaviour
{
    class LootItem
    {
        public ulong id;
        public GameObject lootObject;
        public string type;
    }

    [SerializeField]
    LootableList lootsList;
    private List<LootItem> loots = new List<LootItem>();

    MMSimpleObjectPooler objectPooler;

    [SerializeField]
    GameObject lootPickedVfx;

    void Start()
    {
        for (int i = 0; i < lootsList.LootList.Count; i++)
        {
            this.objectPooler = Utils.SimpleObjectPooler(
                "LootPool",
                transform.parent.parent,
                lootsList.LootList[i].lootPrefab
            );
        }
    }

    private void MaybeAddLoot(Item item)
    {
        if (!ExistInLoots(item.id))
        {
            var position = Utils.transformBackendPositionToFrontendPosition(item.position);
            position.y = 0;
            LootItem LootItem = new LootItem();
            LootItem.lootObject = objectPooler.GetPooledGameObject();
            LootItem.lootObject.transform.position = position;
            LootItem.lootObject.name = item.id.ToString();
            LootItem.lootObject.transform.rotation = Quaternion.identity;
            LootItem.lootObject.SetActive(true);
            LootItem.id = item.id;
            LootItem.type = convertItemNameToType(item.name);
            loots.Add(LootItem);
        }
    }

    private void RemoveLoots(List<Item> updatedLoots)
    {
        loots
            .ToList()
            .ForEach(loot =>
            {
                if (!updatedLoots.Exists(lootPackage => lootPackage.id == loot.id))
                {
                    RemoveLoot(loot.id);
                }
            });
    }

    private void RemoveLoot(ulong id)
    {
        GameObject lootObject = GetLoot(id).lootObject;
        string type = GetLoot(id).type;

        Sound3DManager lootSoundManagerRef = lootObject.GetComponent<Sound3DManager>();
        AudioSource lootAudioSource = lootObject.GetComponent<AudioSource>();
        lootSoundManagerRef.SetSfxSound(GetLootableByType(type).pickUpSound);
        lootSoundManagerRef.PlaySfxSound();

        PlayVfx(lootObject.transform.position);

        lootObject.SetActive(false);
        RemoveById(id);
    }

    private void PlayVfx(Vector3 lootPosition)
    {
        Vector3 position = new Vector3(
            lootPosition.x,
            lootPickedVfx.transform.position.y,
            lootPosition.z
        );
        GameObject feedbackVfx = Instantiate(lootPickedVfx, position, Quaternion.identity);
    }

    private bool ExistInLoots(ulong id)
    {
        return loots.Any(loot => loot.id == id);
    }

    private void RemoveById(ulong id)
    {
        LootItem toRemove = loots.Find(loot => loot.id == id);
        loots.Remove(toRemove);
    }

    private LootItem GetLoot(ulong id)
    {
        return loots.Find(loot => loot.id == id);
    }

    private Lootable GetLootableByType(string type)
    {
        return lootsList.LootList.Find(loot => loot.type == type);
    }

    public void UpdateLoots()
    {
        List<Item> updatedLoots = SocketConnectionManager.Instance.gameState.items;
        RemoveLoots(updatedLoots);
        updatedLoots.ForEach(MaybeAddLoot);
    }

    private string convertItemNameToType(string name) {
        switch (name) {
            case "loot_health": return "LootHealth";
            default: throw new ArgumentException(String.Format("no type for `{0}`", name));
        }
    }
}
