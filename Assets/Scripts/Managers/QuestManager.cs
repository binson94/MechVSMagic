﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

///<summary> 퀘스트 진행 상태 표시 나열형 </summary>
public enum QuestState
{
    NotReceive, Proceeding, CanClear, Fail
}
///<summary> 개별 퀘스트 진행 상황 저장을 위한 클래스
///<para> QuestSlot 클래스에서 진행 중인 퀘스트 정보 리스트 형태로 관리 </para>
///</summary>
public class QuestProceed
{
    ///<summary> 퀘스트 인덱스 </summary>
    public int idx;
    ///<summary> 퀘스트 타입 </summary>
    public QuestType type;
    ///<summary> 퀘스트 목표 종류(ex : 몬스터 인덱스) </summary>
    public int objectIdx;
    ///<summary> 퀘스트 현재 진행 상황 </summary>
    public int objectCurr;
    ///<summary> 퀘스트 목표 요구량 </summary>
    public int objectReq;
    ///<summary> 퀘스트 진행 상태 </summary>
    public QuestState state;

    ///<summary> 기본 생성자 </summary>
    public QuestProceed()
    {
        idx = 0;
        objectCurr = 0;
        state = QuestState.NotReceive;
    }
    ///<summary> 퀘스트 수락 시 사용하는 생성자 </summary>
    public QuestProceed(QuestBlueprint qbp)
    {
        idx = qbp.idx;
        type = qbp.type;
        objectIdx = qbp.objectIdx;
        objectCurr = 0; objectReq = qbp.objectAmt;
        state = QuestState.Proceeding;
    }
    ///<summary> 돌발 퀘스트 수락 시 사용하는 함수 </summary>
    public void AcceptOutbreak(QuestBlueprint qbp)
    {
        idx = qbp.idx;
        type = qbp.type;
        objectIdx = qbp.objectIdx;
        objectCurr = 0; objectReq = qbp.objectAmt;
        state = QuestState.Proceeding;
    }
}

//퀘스트 정보 관리
public class QuestManager
{
    const int QUEST_AMT = 62;
    const int OUTBREAK_AMT = 22;
    static QuestBlueprint[] questData = new QuestBlueprint[QUEST_AMT + 1];
    static QuestBlueprint[] outbreakData = new QuestBlueprint[OUTBREAK_AMT + 1];

    public static void LoadData()
    {
        questData[0] = outbreakData[0] = new QuestBlueprint();

        for (int i = 1; i <= QUEST_AMT; i++)
            questData[i] = new QuestBlueprint(false, i);
        for (int i = 1; i <= OUTBREAK_AMT; i++)
            outbreakData[i] = new QuestBlueprint(true, i);
    }

    ///<summary> 현재 수행 중인 퀘스트 정보 반환 </summary>
    public static List<QuestProceed> GetCurrQuest() => GameManager.Instance.slotData.questData.GetCurrQuest();
    public static KeyValuePair<QuestBlueprint, int>[] GetProceedingQuestData()
    {
        QuestProceed qp;
        KeyValuePair<QuestBlueprint, int>[] list = new KeyValuePair<QuestBlueprint, int>[4];
        int i;
        for(i = 0;i < GameManager.Instance.slotData.questData.proceedingQuestList.Count;i++)
        {
            qp = GameManager.Instance.slotData.questData.proceedingQuestList[i];
            list[i] = new KeyValuePair<QuestBlueprint, int>(questData[qp.idx], qp.objectCurr);
        }
        for(;i < 3;i++)
            list[i] = new KeyValuePair<QuestBlueprint, int>(null, 0);

        qp = GameManager.Instance.slotData.questData.outbreakProceed;
        if(qp.idx > 0)
            list[3] = new KeyValuePair<QuestBlueprint, int>(outbreakData[qp.idx], qp.objectCurr);
        else
            list[3] = new KeyValuePair<QuestBlueprint, int>(null, 0);

        return list;
    }
    public static List<int> GetClearedQuest() => GameManager.Instance.slotData.questData.GetClearedQuest();
    public static string GetQuestName(bool isOutbreak, int questIdx)
    {
        if(isOutbreak)
            return outbreakData[questIdx].name;
        else
            return questData[questIdx].name;
    }
    public static string GetQuestScript(bool isOutbreak, int questIdx)
    {
        if(isOutbreak)
            return outbreakData[questIdx].script;
        else
            return questData[questIdx].script;
    }

    #region QuestProgress
    ///<summary> 퀘스트 진행 업데이트 </summary>
    public static void QuestUpdate(QuestType type, int? idx, int amt)
    {
        GameManager.Instance.slotData.questData.QuestUpdate(type, idx, amt);
        GameManager.Instance.SaveSlotData();
    }
    ///<summary> 체력유지 돌발 퀘스트 업데이트 </summary>
    public static void DiehardUpdate(float rate) => GameManager.Instance.slotData.questData.DiehardUpdate(rate);

    ///<summary> 새 퀘스트 받기, 마을 메뉴 또는 던전 돌발퀘 방에서 호출 </summary>
    public static void AcceptQuest(bool isOutbreak, int questIdx)
    {
        if (isOutbreak)
            GameManager.Instance.slotData.questData.AcceptOutbreak(outbreakData[questIdx]);
        else
            GameManager.Instance.slotData.questData.AcceptQuest(questData[questIdx]);

        GameManager.Instance.SaveSlotData();
    }

    ///<summary> 퀘스트 클리어 </summary>
    public static void ClearQuest(int questIdx)
    {
        GameManager.Instance.slotData.questData.ClearQuest(questIdx);
        GetReward(questData[questIdx]);
        GameManager.Instance.SaveSlotData();
    }
    ///<summary> 돌발 퀘스트 클리어 </summary>
    public static void ClearOutbreak()
    {
        int outbreakIdx = GameManager.Instance.slotData.questData.outbreakProceed.idx;
        if (outbreakIdx > 0)
        {
            GetReward(outbreakData[outbreakIdx]);
            GameManager.Instance.slotData.questData.ClearOutbreak();
        }
        else
            GameManager.Instance.slotData.questData.RemoveOutbreak();

        GameManager.Instance.SaveSlotData();
    }
    ///<summary> 돌발 퀘스트 수행 정보 제거(중도 포기) </summary>
    public static void RemoveOutbreak()
    {
        GameManager.Instance.slotData.questData.RemoveOutbreak();
        GameManager.Instance.SaveSlotData();
    }
    ///<summary> 퀘스트 클리어에 따른 보상 획득
    ///<para> ClearQuest에서 호출</summary>
    static void GetReward(QuestBlueprint qbp)
    {
        GameManager.Instance.questDrops.Clear();
        GameManager.Instance.questExp = 0;
        for (int i = 0; i < qbp.rewardCount; i++)
            ItemManager.ItemDrop(qbp.rewardIdx[i], qbp.rewardAmt[i], true);
        QuestManager.QuestUpdate(QuestType.Level, 0, 0);
    }
    #endregion QuestProgress
}
