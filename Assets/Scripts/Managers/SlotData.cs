﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum SceneKind
{
    Title = 1, Town, Dungeon, Battle, Story
}
public enum DropType
{
    Material, Equip, Skillbook, Recipe
}
public class Pair<T1, T2>
{
    public T1 Key { get; set; }
    public T2 Value { get; set; }

    public Pair() {}
    public Pair(T1 f, T2 s)
    {
        Key = f; Value = s;
    }
}
public class Triplet<T1, T2, T3>
{
    public T1 first;
    public T2 second;
    public T3 third;

    public Triplet() { }
    public Triplet(T1 a, T2 b, T3 c)
    {
        first = a; second = b; third = c;
    }
}
///<summary> 슬롯 데이터 저장용 </summary>
public class SlotData
{
    ///<summary> 현재 슬롯의 직업 </summary>
    public int slotClass;
    public string className;
    public int region;

    public int lvl;
    public int exp;
    ///<summary> 현재 진행 중인 챕터 </summary>
    public int chapter;
    ///<summary> 기본 스텟 + 아이템에 의한 스텟 </summary>
    public int[] itemStats = new int[13];


    ///<summary> 장착한 액티브 스킬 </summary>
    public int[] activeSkills = new int[6];
    ///<summary> 장착한 패시브 스킬 </summary>
    public int[] passiveSkills = new int[4];
    ///<summary> 장착한 포션 </summary>
    public int[] potionSlot = new int[2];

    ///<summary> 재시작 시 로드할 씬 </summary>
    public SceneKind nowScene;
    ///<summary> 스토리 인덱스 </summary>
    public int storyIdx;

    ///<summary> 현재 진행 중인 던전 인덱스 </summary>
    public int dungeonIdx;
    ///<summary> 현재 진행 중인 던전 정보 </summary>
    public DungeonData dungeonData;

    ///<summary> 획득한 아이템들 정보 </summary>
    public ItemData itemData;
    ///<summary> 퀘스트 진행 정보 </summary>
    public QuestData questData;

    ///<summary> 로드 시 사용할 빈 생성자 </summary>
    public SlotData() { }
    ///<summary> 새 슬롯 생성 시 사용할 생성자 </summary>
    public SlotData(int classIdx)
    {
        lvl = 1;
        slotClass = classIdx;
        region = slotClass <= 4 ? 10 : 11;
        switch(slotClass)
        {
            case 1:
                className = "암드파이터";
                break;
            case 2:
                className = "메탈 나이트";
                break;
            case 3:
                className = "블래스터";
                break;
            case 4:
                className = "매드 사이언티스트";
                break;
            case 5:
                className = "엘리멘탈 컨트롤러";
                break;
            case 6:
                className = "드루이드";
                break;
            case 7:
                className = "비전술사";
                break;
            case 8:
                className = "매지컬 로그";
                break;
        }
        chapter = 1;
        storyIdx = 1 + region / 11 * 5;

        for (int i = 0; i <= 12; i++)
            if(slotClass == 4 && i != 4 && i != 7)
                itemStats[i] = Mathf.RoundToInt(GameManager.BaseStats[i] * 0.8f);
            else
                itemStats[i] = GameManager.BaseStats[i];

        nowScene = SceneKind.Story;

        itemData = new ItemData(slotClass);
        questData = new QuestData(slotClass);
        dungeonData = null;

        if (slotClass == 7)
            for (int i = 0, j = 1; i < 6 && j < itemData.learnedSkills.Count; j++)
            {
                Skill skill;
                if ((skill = SkillManager.GetSkill(slotClass, itemData.learnedSkills[j])).category == 1023)
                    activeSkills[i++] = itemData.learnedSkills[j];
            }
        else
            for (int i = 0, j = 1; i < 6 && j < itemData.learnedSkills.Count; i++, j++)
                activeSkills[i] = itemData.learnedSkills[j];

        potionSlot[0] = 3; potionSlot[1] = 1;
    }
    ///<summary> 던전 진행 중 획득한 아이템 정보 저장(결과창 용) </summary>
    public void DropSave(DropType type, int idx, int amt)
    {
        if (dungeonData == null)
            return;

        if (dungeonData.dropList.Any(x=>x.first == type && x.second == idx))
            dungeonData.dropList.FindAll(x=>x.first == type && x.second == idx).First().third += amt;
        else
            dungeonData.dropList.Add(new Triplet<DropType, int, int>(type, idx, amt));
    }
    ///<summary> 경험치 획득 </summary>
    public void GetExp(int amt)
    {
        if (lvl < 10)
        {
            exp += amt;
            if (dungeonData != null)
                dungeonData.dropExp += amt;

            while (lvl < 10 && exp >= GameManager.ReqExp[lvl])
            {
                exp -= GameManager.ReqExp[lvl];
                lvl++;

                if (dungeonData != null)
                    dungeonData.isLvlUp = true;
            }
        }
    }
}
///<summary> 던전 진행 정보 저장용 </summary>
public class DungeonData
{

    #region PositionData
    ///<summary> 던전 맵 스크롤, 전투 후 로드 시 스크롤 상태 불러옴 </summary>
    public double mapScroll;
    ///<summary> 전투 방 번호, 이벤트 번호, 돌발퀘 번호 </summary>
    public int currRoomEvent;
    ///<summary> 던전 맵에서 현재 위치 </summary>
    public int[] currPos;
    ///<summary> 던전 정보 </summary>
    public Dungeon currDungeon;
    #endregion PositionData

    #region BattleData
    ///<summary> 현재 체력 </summary>
    public int currHP;
    ///<summary> 4 매드사이언티스트 골렘 체력 </summary>
    public int golemHP;
    ///<summary> 6 드루이드 부활 여부(255 세계수의 보호) </summary>
    public int druidRevive;
    ///<summary> 포션 사용 여부 </summary>
    public bool[] potionUse = new bool[2];
    #endregion BattleData

    #region EventData
    ///<summary> 이벤트로 발생한 버프 </summary>
    public List<DungeonBuff> dungeonBuffs = new List<DungeonBuff>();
    ///<summary> 이벤트로 발생한 디버프 </summary>
    public List<DungeonBuff> dungeonDebuffs = new List<DungeonBuff>();
    #endregion EventData

    #region DropData
    ///<summary> 드롭된 아이템들 정보
    ///<para> 드롭 타입, 인덱스(장비, 스킬북, 레시피), 갯수 순 </para> </summary>
    public List<Triplet<DropType, int, int>> dropList = new List<Triplet<DropType, int, int>>();
    ///<summary> 드롭된 경험치 정보 </summary>
    public int dropExp = 0;
    ///<summary> 이번 던전에서 레벨업 여부, 보고서 표시용 </summary>
    public bool isLvlUp = false;
    #endregion DropData

    ///<summary> 로드를 위한 빈 생성자 </summary>
    public DungeonData() { }
    ///<summary> 던전 입장 시 사용하는 생성자 </summary>
    public DungeonData(int dungeonIdx)
    {
        currDungeon = new Dungeon(dungeonIdx);
        mapScroll = 0;

        currHP = -1; druidRevive = 0; golemHP = GameManager.SlotClass == 4 ? 0 : -1;
        potionUse[0] = potionUse[1] = false;
        currPos = new int[2] { 0, 0 };
    }

    public Room GetCurrRoom() => currDungeon.GetRoom(currPos[0], currPos[1]);
}
///<summary> 아이템 획득 정보 저장용 </summary>
public class ItemData
{
    ///<summary> 기본 재화 
    ///<para> 1~3 : 스킬 재화(상중하) </para>
    ///<para> 4~12 : 아이템 특수 재화(상중하 우선, 무기,방어구,장신구) </para>
    ///<para> 13~15 : 아이템 공통 재화 </para>
    ///</summary>
    public int[] basicMaterials;
    ///<summary> 장비 레시피 획득 정보 </summary>
    public List<int> equipRecipes = new List<int>();
    ///<summary> 보유 스킬북 정보 </summary>
    public List<Skillbook> skillbooks = new List<Skillbook>();

    ///<summary> 현재 슬롯 직업의 시작 스킬 인덱스 </summary>
    public int skillStartIdx;
    ///<summary> 스킬 학습 여부 </summary>
    public List<int> learnedSkills = new List<int>();

    ///<summary> 획득한 무기 </summary>
    public List<Equipment> weapons = new List<Equipment>();
    ///<summary> 획득한 방어구 </summary>
    public List<Equipment> armors = new List<Equipment>();
    ///<summary> 획득한 장신구 </summary>
    public List<Equipment> accessories = new List<Equipment>();
    List<Equipment> GetEquipmentList(EquipPart part)
    {
        switch (part)
        {
            case EquipPart.Weapon:
                return weapons;
            case EquipPart.Top:
            case EquipPart.Pants:
            case EquipPart.Gloves:
            case EquipPart.Shoes:
                return armors;
        }
        return accessories;
    }
    ///<summary> 장착 중인 장비
    ///<para> 1 무기, 2 상의, 3 하의, 4 장갑, 5 신발, 6 반지, 7 목걸이 </para>
    ///</summary>
    public Equipment[] equipmentSlots = new Equipment[8];

    #region Smith
    ///<summary> 장비 제작 </summary>
    public Equipment Create(EquipBluePrint ebp)
    {
        for (int i = 0; i < ebp.requireResources.Count; i++)
            basicMaterials[ebp.requireResources[i].Key] -= ebp.requireResources[i].Value;
        return EquipDrop(ebp);
    }
    ///<summary> 장비 분해 </summary>
    public void Disassemble(KeyValuePair<int, Equipment> equipInfo)
    {
        List<Equipment> eList = GetEquipmentList(equipInfo.Value.ebp.part);
        GetResource(eList[equipInfo.Key]);
        eList.RemoveAt(equipInfo.Key);

        //분해 시 획득 재료 = 제작 시 소모 재료의 20% * 2^(장비 star)
        void GetResource(Equipment e)
        {
            for (int i = 0; i < e.ebp.requireResources.Count; i++)
                basicMaterials[e.ebp.requireResources[i].Key] += Mathf.RoundToInt(Mathf.Pow(2, e.star) * 0.2f * e.ebp.requireResources[i].Value);
        }
    }
    ///<summary> 장비 융합 </summary>
    public void Merge(KeyValuePair<int, Equipment> equipInfo, KeyValuePair<int, Equipment> resourceInfo)
    {
        List<Equipment> baseList;
        if(resourceInfo.Value.ebp.part <= EquipPart.Weapon)
            baseList = weapons;
        else if(resourceInfo.Value.ebp.part <= EquipPart.Shoes)
            baseList = armors;
        else
            baseList = accessories;

        equipInfo.Value.Merge();
        baseList.RemoveAt(resourceInfo.Key);
        baseList.Sort((a, b) => a.CompareTo(b));
    }
    #endregion Smith

    #region Drop
    ///<summary> 새로운 장비 드롭 </summary>
    public Equipment EquipDrop(EquipBluePrint ebp)
    {
        List<Equipment> eList = GetEquipmentList(ebp.part);

        Equipment e = new Equipment(ebp);
        eList.Add(e);
        eList.Sort((a, b) => a.CompareTo(b));
        return e;
    }
    ///<summary> 새로운 스킬북 드롭 </summary>
    public void SkillBookDrop(int skillIdx)
    {
        int left = 0; int right = skillbooks.Count - 1;
        int mid = 0;

        while(left <= right)
        {
            mid = (left + right) / 2;
            if(skillbooks[mid].idx < skillIdx)
                right = mid - 1;
            else if(skillbooks[mid].idx == skillIdx)
            {
                skillbooks[mid].count++;
                return;
            }
            else
                left = mid + 1;
        }
        skillbooks.Insert(left, new Skillbook(skillIdx, 1));
    }
    ///<summary> 새로운 장비 레시피 드롭, 이미 레시피 가지고 있으면 false 반환 </summary>
    public bool RecipeDrop(int equipIdx)
    {
        if(!equipRecipes.Contains(equipIdx))
        {
            equipRecipes.Add(equipIdx);
            equipRecipes.Sort();
            return true;
        }
        return false;
    }
    #endregion Drop
    
    ///<summary> 장비 장착 </summary>
    public void Equip(EquipPart part, int orderIdx)
    {
        bool sort = false;
        List<Equipment> eList = GetEquipmentList(part);

        //이미 장착한 장비 있음 -> 장착 해제
        if (equipmentSlots[(int)part] != null)
        {
            sort = true;
            eList.Add(equipmentSlots[(int)part]);
            equipmentSlots[(int)part] = null;
        }

        //장비 장착
        equipmentSlots[(int)part] = eList[orderIdx];
        eList.RemoveAt(orderIdx);

        if (sort) eList.Sort((a, b) => a.CompareTo(b));
    }
    ///<summary> 장비 해제 </summary>
    public void UnEquip(EquipPart part)
    {
        if (equipmentSlots[(int)part] != null)
        {
            List<Equipment> eList = GetEquipmentList(part);
            eList.Add(equipmentSlots[(int)part]);
            eList.Sort((a, b) => a.CompareTo(b));

            equipmentSlots[(int)part] = null;
        }
    }

    #region Skill
    ///<summary> 스킬 학습 여부 반환 </summary>
    public bool IsLearned(int skillIdx)
    {
        //선행 스킬 없으면 0으로 표기 -> 무조건 참 반환
        if (skillIdx < skillStartIdx)
            return true;
        return learnedSkills.Contains(skillIdx);
    }
    ///<summary> 스킬 학습 </summary>
    public void SkillLearn(KeyValuePair<int, Skillbook> skillInfo)
    { 
        learnedSkills.Add(skillInfo.Value.idx);
        if(--skillbooks[skillInfo.Key].count <= 0)
            skillbooks.RemoveAt(skillInfo.Key);
    }
    ///<summary> 스킬북 분해 </summary>
    public void DisassembleSkillbook(int slotIdx)
    {
        if(--skillbooks[slotIdx].count <= 0)
            skillbooks.RemoveAt(slotIdx);
    }
    ///<summary> 스킬북 보유 여부 반환 </summary>
    public bool HasSkillBook(int skillIdx) => skillbooks.Any(x => x.idx == skillIdx);
    #endregion Skill

    ///<summary> 로드용 빈 생성자 </summary>
    public ItemData() { }
    public ItemData(int currClass)
    {
        basicMaterials = new int[16];

        Skill[] s = SkillManager.GetSkillData(currClass);

        //1레벨 액티브 스킬은 학습 상태로 시작
        skillStartIdx = s[0].idx;
        learnedSkills.Add(0);
        for(int i = 0;s[i].reqLvl == 1 && s[i].useType == 0;i++)
            learnedSkills.Add(s[i].idx);
    }
}
///<summary> 퀘스트 진행 정보 저장용 </summary>
public class QuestData
{
    ///<summary> 이미 클리어 한 퀘스트 인덱스들 </summary>
    public List<int> clearedQuestList = new List<int>();
    ///<summary> 현재 진행 중인 퀘스트 정보(proceeding, canClear) </summary>
    public List<QuestProceed> proceedingQuestList = new List<QuestProceed>();

    ///<summary> 현재 진행 중인 돌발 퀘스트 정보 </summary>
    public QuestProceed outbreakProceed;

    ///<summary> 데이터 로드용 빈 생성자 </summary>
    public QuestData() { }
    ///<summary> 빈 생성자와 구별하기 위한 매개변수 사용 </summary>
    public QuestData(int slotClass)
    {
        clearedQuestList.Add(0);

        outbreakProceed = new QuestProceed();
    }

    ///<summary> 퀘스트 수락 </summary>
    public void AcceptQuest(QuestBlueprint qbp)
    {
        if (proceedingQuestList.Count < 3)
            proceedingQuestList.Add(new QuestProceed(qbp));
        QuestUpdate(QuestType.Level, 0, 0);
    }
    ///<summary> 적 처치 등 퀘스트 요구 사항 관련 변경점 있을 때마다 호출 </summary>
    public void QuestUpdate(QuestType type, int? objectIdx, int amt)
    {
        if(objectIdx is null) return;
        
        foreach (QuestProceed qp in proceedingQuestList)
        {
            //레벨 달성 퀘스트인 경우, 레벨 값으로 설정
            if (qp.state == QuestState.Proceeding && qp.type == QuestType.Level)
            {
                qp.objectCurr = GameManager.SlotLvl;
                if (qp.objectCurr >= qp.objectReq)
                    qp.state = QuestState.CanClear;
            }
            else if (qp.state == QuestState.Proceeding && qp.type == type)
            {
                //그 외 경우, 대상 일치 시 퀘스트 증가, 원숭이 로봇, 탈리아 퀘스트 예외 처리
                if ((qp.objectIdx == 0 || qp.objectIdx == objectIdx) ||
                        (qp.objectIdx == 72 && (objectIdx == 73 || objectIdx == 74)) ||
                        (qp.objectIdx == 83 && (objectIdx == 84 || objectIdx == 85)))
                    qp.objectCurr += amt;

                //목표 달성 시 클리어 처리
                if (qp.objectCurr >= qp.objectReq)
                    qp.state = QuestState.CanClear;
            }
        }

        //돌발 퀘스트
        if (outbreakProceed.state == QuestState.Proceeding && outbreakProceed.type == type)
        {
            if (!(outbreakProceed.type == QuestType.Diehard_Over || outbreakProceed.type == QuestType.Diehard_Under))
                if (outbreakProceed.objectIdx == 0 || outbreakProceed.objectIdx == objectIdx)
                    outbreakProceed.objectCurr++;

            //클리어 처리
            if (outbreakProceed.objectCurr >= outbreakProceed.objectReq)
                outbreakProceed.state = QuestState.CanClear;
        }
    }
    ///<summary> 전투 끝날 때마다 호출 </summary>
    public void DiehardUpdate(float rate)
    {
        if (outbreakProceed.state == QuestState.Proceeding)
            if ((outbreakProceed.type == QuestType.Diehard_Over && 100 * rate < outbreakProceed.objectReq) || (outbreakProceed.type == QuestType.Diehard_Under && 100 * rate > outbreakProceed.objectReq))
                outbreakProceed.state = QuestState.Fail; 
            
    }
    ///<summary> 퀘스트 클리어 판정 </summary>
    public void ClearQuest(int idx)
    {
        clearedQuestList.Add(idx);
        proceedingQuestList.RemoveAll(x => x.idx == idx && x.state == QuestState.CanClear);
    }

    ///<summary> 돌발 퀘스트 수락 </summary>
    public void AcceptOutbreak(QuestBlueprint qbp) => outbreakProceed.AcceptOutbreak(qbp);
    ///<summary> 돌발 퀘스트 완료 </summary>
    public void ClearOutbreak()
    {
        QuestUpdate(QuestType.Outbreak, outbreakProceed.idx, 1);
        RemoveOutbreak();
    }
    ///<summary> 돌발 퀘스트 제거 </summary>
    public void RemoveOutbreak()
    {
        outbreakProceed.idx = outbreakProceed.objectCurr = 0;
        outbreakProceed.state = QuestState.NotReceive;
    }

    ///<summary> 현재 퀘스트 진행 상태 보여주기 위해 반환 </summary>
    public List<QuestProceed> GetCurrQuest() => proceedingQuestList;
    public List<int> GetClearedQuest() => clearedQuestList;
    public int GetOutbreakIdx()
    {
        if (outbreakProceed.state == QuestState.CanClear)
            return outbreakProceed.idx;
        else
            return -1;
    }
}