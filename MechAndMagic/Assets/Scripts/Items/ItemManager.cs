﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using LitJson;


public class ItemManager : MonoBehaviour
{
    static SetOptionManager setManager;

    public const int EQUIP_COUNT = 567;
    
    //모든 장비 정보
    static EquipBluePrint[] bluePrints = new EquipBluePrint[EQUIP_COUNT];
    static Potion[] potions = new Potion[18];

    private void Awake()
    {
        for (int i = 0; i < EQUIP_COUNT; i++)
            bluePrints[i] = new EquipBluePrint(i);
        for(int i = 0; i < 18;i++)
            potions[i] = new Potion(i + 1);
        
        setManager = new SetOptionManager();
    }

    #region ItemDrop
    public static void ItemDrop(int category, float prob)
    {
        if(category == 150)
            GameManager.GetExp((int)prob);
        else if (prob >= 1f)
        {
            for (float i = 0; i < prob; i += 1f)
                AddItem();
        }
        else if (Random.Range(0, 1f) < prob)
        {
            AddItem();
        }

        GameManager.SaveSlotData();

        void AddItem()
        {
            //기본 재료
            if (category <= 15)
            {
                GameManager.slotData.itemData.basicMaterials[category]++;
                GameManager.DropSave(DropType.Material, category);
            }
            //포션
            else if (category <= 18)
                NewPotion(category);
            //스킬북
            else if (category <= 23)
                NewSkillBook(category);
            //장비
            else if (category <= 86)
                NewEquip(GameManager.slotData.slotClass, category);
            //제작법
            else if (category <= 149)
                NewEquipRecipe(GameManager.slotData.slotClass, category);
        }
    }
    static void NewPotion(int idx) => GameManager.slotData.itemData.potions[idx] = 1;
    static void NewEquip(int classIdx, int category)
    {
        int region = (classIdx < 5) ? 10 : 11;
        Debug.Log(string.Concat(classIdx, " ", category, " ", region));
        var possibleList = (from token in bluePrints
                            where (token.category == category) && (token.useClass == classIdx || token.useClass == region || token.useClass == 0)
                            select token);

        if (possibleList.Count() <= 0)
            return;

        EquipBluePrint ebp = possibleList.Skip(Random.Range(0, possibleList.Count())).Take(1).First();

        GameManager.DropSave(DropType.Equip, ebp.idx);
        GameManager.slotData.itemData.EquipDrop(ebp);
    }
    static void NewSkillBook(int category)
    {
        int lvl = 47 - 2 * category;

        List<Skillbook> possibleList = (from token in GameManager.slotData.itemData.skillbooks
                            where (lvl == 0 || lvl == token.lvl)
                            select token).ToList();

        if (possibleList.Count() <= 0)
            return;

        int idx = Random.Range(0, possibleList.Count());
        possibleList[idx].count += 1;

        GameManager.DropSave(DropType.Skillbook, idx);
        GameManager.SaveSlotData();
    }
    static void NewEquipRecipe(int classIdx, int category)
    {
        category -= 63;
        int region = (classIdx < 5) ? 10 : 11;
        var possibleList = (from token in bluePrints
                            where (token.category == category) && (token.useClass == classIdx || token.useClass == region || token.useClass == 0)
                            select token);

        if (possibleList.Count() <= 0)
            return;

        int idx = possibleList.Skip(Random.Range(0, possibleList.Count())).Take(1).First().idx;

        GameManager.slotData.itemData.equipRecipes[idx] += 1;
        GameManager.DropSave(DropType.Recipe, bluePrints[idx].idx);

        GameManager.SaveSlotData();
    }
    #endregion

    #region Smith
    public static bool CanSmith(int idx) => GameManager.slotData.itemData.CanSmith(bluePrints[idx]);
    public static bool CanSwitchOption(EquipPart part, int idx) => GameManager.slotData.itemData.CanSwitchCommonStat(part, idx);
    public static bool CanFusion(EquipPart part, int idx) => GameManager.slotData.itemData.CanFusion(part, idx);
    
    public static bool IsLearned(int idx) => GameManager.slotData.itemData.IsLearned(idx);

    public static void SmithEquipment(int idx)
    {
        GameManager.slotData.itemData.Smith(bluePrints[idx]);
        GameManager.SaveSlotData();
    }
    public static void DisassembleEquipment(EquipPart part, int idx)
    {
        GameManager.slotData.itemData.Disassemble(part, idx);
        GameManager.SaveSlotData();
    }
    public static void SwitchEquipOption(EquipPart part, int idx)
    {
        GameManager.slotData.itemData.SwitchCommonStat(part, idx);
        GameManager.SaveSlotData();
    }
    public static void FusionEquipment(EquipPart part, int idx)
    {
        GameManager.slotData.itemData.Fusion(part, idx);
        GameManager.SaveSlotData();
    }

    public static void SkillLearn(int idx)
    {
        GameManager.slotData.itemData.SkillLearn(idx);
        GameManager.SaveSlotData();
    }
    public static void DisassembleSkillBook(int idx)
    {
        GameManager.slotData.itemData.DisassembleSkillbook(idx);
        GameManager.SaveSlotData();
    }
    #endregion

    #region Equip
    public static void Equip(EquipPart part, int idx) 
    {
        GameManager.slotData.itemData.Equip(part, idx);
        ItemStatUpdate();
        setManager.SetComfirm(GameManager.slotData.itemData);
        GameManager.SaveSlotData();
    }
    public static void UnEquip(EquipPart part)
    {
        GameManager.slotData.itemData.UnEquip(part);
        ItemStatUpdate();
        setManager.SetComfirm(GameManager.slotData.itemData);
        GameManager.SaveSlotData();
    }
    static void ItemStatUpdate()
    {
        int[] addPivots = new int[13];
        foreach (Equipment e in GameManager.slotData.itemData.equipmentSlots)
            if (e != null)
            {
                addPivots[(int)e.mainStat] += e.mainStatValue;
                addPivots[(int)e.subStat] += e.subStatValue;

                for(int i = 0;i < e.commonStatValue.Count;i++)
                    addPivots[(int)e.commonStatValue[i].Key] += e.commonStatValue[i].Value;
            }

        for(int i = 1;i<13;i++)
        {
            GameManager.slotData.itemStats[i] = SlotData.baseStats[i] + addPivots[i];
        }
        GameManager.slotData.itemStats[1] = GameManager.slotData.itemStats[2];
        GameManager.slotData.itemStats[3] = GameManager.slotData.itemStats[4];
        GameManager.SaveSlotData();
    }
    public static int[] GetStatDelta(Equipment newE)
    {
        int[] addPivots = new int[13];

        addPivots[(int)newE.mainStat] += newE.mainStatValue;
        addPivots[(int)newE.subStat] += newE.subStatValue;
        for(int i = 0; i<newE.commonStatValue.Count;i++)
            addPivots[(int)newE.commonStatValue[i].Key] += newE.commonStatValue[i].Value;

        Equipment currE = GameManager.slotData.itemData.equipmentSlots[(int)newE.ebp.part - 1];
        if(currE != null)
        {
            addPivots[(int)currE.mainStat] -= currE.mainStatValue;
            addPivots[(int)currE.subStat] -= currE.subStatValue;
            for(int i =0;i<currE.commonStatValue.Count;i++)
                addPivots[(int)currE.commonStatValue[i].Key] -= currE.commonStatValue[i].Value;
        }
        
        int[] ret = new int[10];

        for(int i = 2, j = 0;i < 13;i++)
        {
            if(i == 3) i++;
            ret[j++] = addPivots[i];
        }

        return ret;
    }
    public static void EquipPotion(int slot, int idx)
    {
        GameManager.slotData.potionSlot[slot] = idx;
        if(GameManager.slotData.potionSlot[(slot + 1) % 2] == idx)
            GameManager.slotData.potionSlot[(slot + 1) % 2] = 0;
    }
    #endregion

    #region Show
    //SmithManager에게 보유한 장비, 스킬북, 자원 정보 주는 함수
    public static List<Equipment> GetEquipData(ItemCategory category, Rarity rarity, int lvl)
    {
        List<Equipment> tmp;
        switch(category)
        {
            case ItemCategory.Weapon:
                tmp = GameManager.slotData.itemData.weapons;
                break;
            case ItemCategory.Armor:
                tmp = GameManager.slotData.itemData.armors;
                break;
            case ItemCategory.Accessory:
                tmp = GameManager.slotData.itemData.accessories;
                break;
            default:
                return null;
        }
        return (from x in tmp
                where (rarity == Rarity.None || (x.ebp.rarity == rarity)) && (lvl == 0 || (x.ebp.reqlvl == lvl))
                select x).ToList();
    }
    public static List<Skillbook> GetSkillbookData(int skillType, int lvl)
    {
        return (from x in GameManager.slotData.itemData.skillbooks
                where ((x.count > 0) && (skillType == -1 || x.type == skillType) && (lvl == 0 || x.lvl == lvl))
                select x).ToList();
    }
    public static List<EquipBluePrint> GetRecipeData(Rarity rarity, int lvl)
    {
        int region = (GameManager.slotData.slotClass < 5) ? 10 : 11;
        List<EquipBluePrint> ebps = new List<EquipBluePrint>();

        for (int i = 0; i < bluePrints.Length; i++)
        {
            if ((GameManager.slotData.itemData.equipRecipes[i] > 0) &&
                (bluePrints[i].useClass == 0 || bluePrints[i].useClass == GameManager.slotData.slotClass || bluePrints[i].useClass == region) &&
                (rarity == Rarity.None || bluePrints[i].rarity == rarity) &&
                (lvl == 0 || bluePrints[i].reqlvl == lvl))
                ebps.Add(bluePrints[i]);
        }

        return ebps;
    }
    public static int[] GetResourceData(ItemCategory category)
    {
        if (category == ItemCategory.Recipe)
            return GameManager.slotData.itemData.equipRecipes;
        else
            return GameManager.slotData.itemData.basicMaterials;
    }

    public static Equipment GetEquipment(EquipPart p)
    {
        return GameManager.slotData.itemData.equipmentSlots[(int)p - 1];
    }
    public static int[] GetSkillbookData()
    {
        return (from a in GameManager.slotData.itemData.skillbooks
               select a.count).ToArray();
    }
    
    public static KeyValuePair<string, float[]> GetSetData(int set) => setManager.GetSetData(set);
    public static Potion GetPotion(int potionIdx) => potions[Mathf.Max(0, potionIdx - 1)];
    #endregion
}

public class SetOptionManager
{

    Dictionary<int, int> setList =new Dictionary<int, int>();
    SetOption[] options;
    public SetOptionManager()
    {
        TextAsset jsonTxt = Resources.Load<TextAsset>("Jsons/Items/SetOption");
        JsonData json = JsonMapper.ToObject(jsonTxt.text);

        options = new SetOption[json.Count];

        for (int i = 0; i < options.Length; i++)
        {
            options[i] = new SetOption();

            options[i].name = json[i]["name"].ToString();
            options[i].setIdx = (int)json[i]["set"];
            options[i].count = (int)json[i]["count"];

            options[i].rate = new float[options[i].count];
            options[i].reqPart = new int[options[i].count];
            for (int j = 0; j < options[i].count; j++)
            {
                options[i].reqPart[j] = (int)json[i]["reqPart"][j];
                options[i].rate[j] = float.Parse(json[i]["rate"][j].ToString());
            }
        }
    }
    public void SetComfirm(ItemData itemData)
    {
        setList.Clear();
        Dictionary<int, int> count = new Dictionary<int, int>();

        for (int i = 0; i < itemData.equipmentSlots.Length; i++)
        {
            if (itemData.equipmentSlots[i] != null)
            {
                int set = itemData.equipmentSlots[i].ebp.set;
                if (set != 0)
                    if (count.ContainsKey(set))
                        count[set]++;
                    else
                        count.Add(set, 1);
            }
        }

        foreach (KeyValuePair<int, int> token in count)
            if (token.Value >= 2)
                setList.Add(token.Key, token.Value);
    }
    public KeyValuePair<string, float[]> GetSetData(int set)
    {
        set--;
        float[] tmp = new float[options[set].count];

        if (setList.ContainsKey(set))
            for (int i = 0; i < options[set].count; i++)
                tmp[i] = setList[set] >= options[set].reqPart[i] ? options[set].rate[i] : 0;

        return new KeyValuePair<string, float[]>(options[set].name, tmp);
    }

    class SetOption
    {
        public string name;
        public int setIdx;
        public int count;
        public int[] reqPart;
        public float[] rate;
    }
}
