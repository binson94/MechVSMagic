﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elemental : Unit
{
    ///<summary> 정령 타입(불 1007, 물 1008, 바람 1009) </summary>
    public int type;
    ///<summary> 강화 정령 </summary>
    public bool isUpgraded;

    int pattern = 0;

    public void Summon(BattleManager bm, ElementalController ec, int type, bool upgrade = false)
    {
        BM = bm;
        isUpgraded = upgrade;
        this.type = type;
        LVL = ec.LVL;

        StatLoad(ec);
        SkillBuff(ec);
        StatUpdate_Turn();
        buffStat[(int)Obj.currHP] = buffStat[(int)Obj.체력];

        SkillSet();
    }

    public override void OnTurnStart()
    {
        base.OnTurnStart();

        if(!IsStun())
            ElementalSkill();
    }

    void SkillSet()
    {
        int pivot = isUpgraded ? 5 : -1;
        for (int i = 0; i < 2; i++)
            activeIdxs[i] = (type - 1006) * 2 + i + pivot;
    }
    void ElementalSkill()
    {
        ActiveSkill(pattern++, new List<Unit>());
        pattern = pattern ^ 1;
    }


    void StatLoad(ElementalController ec)
    {
        dungeonStat[0] = 1;
        for (Obj i = Obj.체력; i <= Obj.속도; i++)
            if(isUpgraded)
            {
                switch(i)
                {
                    case Obj.체력:
                    case Obj.공격력:
                    case Obj.방어력:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.6f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.명중률:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.7f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.회피율:
                    case Obj.속도:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.8f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.치명타율:
                    case Obj.치명타피해:
                    case Obj.방어력무시:
                        dungeonStat[(int)i] = ec.dungeonStat[(int)i];
                        break;
                }
            }
            else
            {
                switch(i)
                {
                    case Obj.체력:
                    case Obj.공격력:
                    case Obj.방어력:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.4f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.명중률:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.6f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.속도:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.7f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.회피율:
                    case Obj.치명타율:
                    case Obj.치명타피해:
                        dungeonStat[(int)i] = Mathf.RoundToInt(0.8f * ec.dungeonStat[(int)i]);
                        break;
                    case Obj.방어력무시:
                        dungeonStat[(int)i] = ec.dungeonStat[(int)i];
                        break;
                }
            }

        dungeonStat[1] = dungeonStat[2];
    }
    void SkillBuff(ElementalController ec)
    {
        //정령의 대리인 2세트 - 정령 힘, 생명 부여 강화
        float rate = 1 + ItemManager.GetSetData(13).Value[0];

        //194 정령 힘 부여
        if (ec.HasSkill(194))
        {
            Skill s= SkillManager.GetSkill(5, 194);
            turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(ec), s.name, s.effectObject[0], ec.buffStat[s.effectStat[0]], s.effectRate[0] * rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
        }
        //195 정령 생명 부여
        if (ec.HasSkill(195))
        {
            Skill s= SkillManager.GetSkill(5, 195);
            turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(ec), s.name, s.effectObject[0], ec.buffStat[s.effectStat[0]], s.effectRate[0] * rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
        }
        if (ec.HasSkill(202) && type == 1007)
            AddBuff(ec, -2, SkillManager.GetSkill(5, 114), 1, 0);
        if (ec.HasSkill(203) && type == 1008)
            AddBuff(ec, -2, SkillManager.GetSkill(5, 115), 1, 0);
        if (ec.HasSkill(204) && type == 1009)
            AddBuff(ec, -2, SkillManager.GetSkill(5, 116), 1, 0);
    }
}
