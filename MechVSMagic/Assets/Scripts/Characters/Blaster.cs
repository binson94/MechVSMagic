﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ImplantBomb
{
    public Unit caster;
    public float dmg;
    public int pen;

    public ImplantBomb(Unit u = null, float d = 0, int p = 0)
    {
        caster = u;
        dmg = d;
        pen = p;
    }
}

public class Blaster : Character
{
    public int currHeat;
    bool beforeCool = false;

    public override void OnTurnStart()
    {
        base.OnTurnStart();
        
        KeyValuePair<string, float[]> set = ItemManager.GetSetData(9);
        //혹한기 작전 4세트 - 이전 턴에 쿨링 히트 시 ATK, ACC, CRC 상승
        if(set.Value[2] > 0 && beforeCool)
        {
            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), set.Key, (int)Obj.ATK, 1, set.Value[2], 1, 1, 1, 1));
            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), set.Key, (int)Obj.ACC, 1, set.Value[2], 1, 1, 1, 1));
            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), set.Key, (int)Obj.CRC, 1, set.Value[2], 1, 1, 1, 1));
        }

        if (currHeat >= 4)
        {
            //무한동력 4세트 - 쿨링히트 되지 않음
            if (ItemManager.GetSetData(7).Value[1] == 0)
            {
                turnDebuffs.Add(new Buff(BuffType.None, LVL, new BuffOrder(this, orderIdx), "쿨링히트", 0, 0, 0, 0, 0, 0, 1));

                //혹한기 작전 2세트 - 쿨링 히트시 30% 회복
                if(ItemManager.GetSetData(9).Value[0] > 0)
                    GetHeal(0.3f * buffStat[(int)Obj.HP]);

                //106 악천후 회피 - 회피율 상승
                if (HasSkill(106))
                {
                    Skill s = SkillManager.GetSkill(classIdx, 147);
                    AddBuff(this, orderIdx, s, 0, 0);
                }
                //115 바이오 쿨링 - 디버프 해제
                if (HasSkill(115))
                    RemoveDebuff(turnDebuffs.Count);

                beforeCool = true;
            }
            else
                beforeCool = false;
        }
        else
            beforeCool = false;
        //126 용광로 코어
        if (HasSkill(126))
            GetHeat(1);
        //109 캐논 예열
        if (HasSkill(109) && currHeat > 0)
        {
            Skill tmp = SkillManager.GetSkill(3, 109);
            //무한동력 2세트 - 캐논 예열이 모든 열기 소모
            int count = ItemManager.GetSetData(7).Value[0] > 0 ? currHeat : 1;
            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), tmp.name, tmp.effectObject[0], count, tmp.effectRate[0], tmp.effectCalc[0], tmp.effectTurn[0], tmp.effectDispel[0], tmp.effectVisible[0]));
            LoseHeat(count);
        }
        //125 극도로 정밀한 계산
        if (HasSkill(125))
        {
            AddBuff(this, orderIdx, SkillManager.GetSkill(classIdx, 125), 0, 0);
        }
    }

    public override string CanCastSkill(int idx)
    {
        if (SkillManager.GetSkill(classIdx, activeIdxs[idx]).category == 1016 && turnDebuffs.buffs.Any(x => x.name == "쿨링히트"))
            return "공격 스킬 사용 불가";
        return base.CanCastSkill(idx);
    }
    public override void ActiveSkill(int idx, List<Unit> selects)
    {
        //적중 성공 여부
        isAcc = true;
        //크리티컬 성공 여부
        isCrit = false;


        //skillDB에서 스킬 불러오기
        Skill skill = SkillManager.GetSkill(classIdx, activeIdxs[idx]);

        skillBuffs.Clear();
        skillDebuffs.Clear();

        if (skill == null)
        {
            Debug.LogError("skill is null");
            return;
        }

        Passive_SkillCast(skill);

        //skill 효과 순차적으로 계산
        Active_Effect(skill, selects);

        //109 캐논 예열, 125 극도로 정밀한 계산 - 매 턴 처음 스킬에만 적용
        if (HasSkill(109))
            turnBuffs.buffs.RemoveAll(x => x.name == SkillManager.GetSkill(classIdx, 109).name);
        if (HasSkill(125))
            turnBuffs.buffs.RemoveAll(x => x.name == SkillManager.GetSkill(classIdx, 125).name);

        orderIdx++;
        buffStat[(int)Obj.currAP] -= GetSkillCost(skill);
        cooldowns[idx] = skill.cooldown;

        //중화기 전문가 5세트 - 쿨타임 3턴 스킬 1턴 감소
        if(skill.cooldown == 3 && ItemManager.GetSetData(8).Value[1] > 0)
            cooldowns[idx]--;
    }
    protected override void Active_Effect(Skill skill, List<Unit> selects)
    {
        List<Unit> effectTargets;
        List<Unit> damaged = new List<Unit>();
        float stat;

        for (int i = 0; i < skill.effectCount; i++)
        {
            effectTargets = GetEffectTarget(selects, damaged, skill.effectTarget[i]);
            stat = GetEffectStat(selects, skill.effectStat[i]);

            switch ((SkillType)skill.effectType[i])
            {
                //데미지 - 스킬 버프 계산 후 
                case SkillType.Damage:
                    {
                        StatUpdate_Skill(skill);

                        float dmg = stat * skill.effectRate[i];

                        damaged.Clear();
                        foreach (Unit u in effectTargets)
                        {
                            if (!u.isActiveAndEnabled)
                                continue;

                            int acc = 20;
                            if (buffStat[(int)Obj.ACC] >= u.buffStat[(int)Obj.DOG])
                                acc = 60 + 6 * (buffStat[(int)Obj.ACC] - u.buffStat[(int)Obj.DOG]) / (u.LVL + 2);
                            else
                                acc = Mathf.Max(acc, 60 + 6 * (buffStat[(int)Obj.ACC] - u.buffStat[(int)Obj.DOG]) / (LVL + 2));

                            if (Random.Range(0, 100) < acc)
                            {
                                isAcc = true;
                                //크리티컬 연산 - dmg * CRB
                                //119 네이팜 탄 - 화상 상대 100% 크리
                                int crt = (skill.idx == 119 && u.turnDebuffs.buffs.Any(x => x.objectIdx.Any(y => y == (int)Obj.Burn)) ? 0 : Random.Range(0, 100));
                                isCrit = crt <= buffStat[(int)Obj.CRC];

                                //123 타이탄 킬러 - 적 체력 비례 추뎀
                                if (HasSkill(123))
                                {
                                    Skill tmp = SkillManager.GetSkill(classIdx, 164);
                                    u.GetDamage(this, dmg + u.buffStat[(int)Obj.HP] * tmp.effectRate[0], buffStat[(int)Obj.PEN], isCrit ? buffStat[(int)Obj.CRB] : 100);
                                }
                                else
                                    u.GetDamage(this, dmg, buffStat[(int)Obj.PEN], isCrit ? buffStat[(int)Obj.CRB] : 100);

                                //112 임플란트 봄
                                if (skill.idx == 112)
                                    u.implantBomb = new ImplantBomb(this, buffStat[(int)Obj.ATK] * 1.5f, buffStat[(int)Obj.PEN]);
                                damaged.Add(u);

                                Passive_SkillHit(skill);
                            }
                            else
                            {
                                isAcc = false;
                                LogManager.instance.AddLog(string.Concat(u.name, "은 ", this.name, "의 공격을 회피했다."));
                            }
                        }

                        break;
                    }
                case SkillType.Heal:
                    {
                        float heal = stat * skill.effectRate[i];

                        foreach (Unit u in effectTargets)
                            u.GetHeal(skill.effectCalc[i] == 1 ? heal * u.buffStat[(int)Obj.HP] : heal);
                        break;
                    }
                case SkillType.Active_Buff:
                    {
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)
                            foreach (Unit u in effectTargets)
                                u.AddBuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case SkillType.Active_Debuff:
                    {
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)
                            foreach (Unit u in effectTargets)
                                u.AddDebuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case SkillType.Active_RemoveBuff:
                    {
                        int count = 0;
                        foreach (Unit u in effectTargets)
                            count += u.RemoveBuff(Mathf.RoundToInt(skill.effectRate[i]));

                        if (HasSkill(149))
                        {
                            Skill tmp = SkillManager.GetSkill(classIdx, 149);
                            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), tmp.name, tmp.effectObject[0], count, tmp.effectRate[0], tmp.effectCalc[0], tmp.effectTurn[0], tmp.effectDispel[0], tmp.effectVisible[0]));
                        }
                        break;
                    }
                case SkillType.Active_RemoveDebuff:
                    {
                        foreach (Unit u in effectTargets)
                            u.RemoveDebuff(Mathf.RoundToInt(skill.effectRate[i]));
                        break;
                    }
                case SkillType.CharSpecial1:
                    {
                        //TP 감소
                        BM.ReduceTP(effectTargets, Mathf.RoundToInt(skill.effectRate[i]));
                        break;
                    }
                case SkillType.CharSpecial2:
                    {
                        //열기 상승
                        GetHeat(skill.effectRate[i]);
                        break;
                    }
                case SkillType.CharSpecial3:
                    {
                        //열기 감소
                        LoseHeat(skill.effectRate[i]);
                        break;
                    }
                default:
                    break;
            }
        }
    }

    protected override void Passive_SkillCast(Skill active)
    {
        for (int j = 0; j < passiveIdxs.Length; j++)
        {
            Skill skill = SkillManager.GetSkill(classIdx, passiveIdxs[j]);

            //124 숙련된 포격술 - 쿨타임 3 스킬 강화
            if (skill.idx == 124 && active.cooldown == 3)
            {
                Skill tmp = SkillManager.GetSkill(classIdx, 124);
                //중화기 전문가 3세트 - 숙련된 포격술 강화
                float rate = ItemManager.GetSetData(8).Value[0];
                for (int i = 0; i < 3; i++)
                    skillBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(), "", tmp.effectObject[i], tmp.effectStat[i], tmp.effectRate[i] + rate, tmp.effectCalc[0], tmp.effectTurn[0]));
                continue;
            }

            for (int i = 0; i < skill.effectCount; i++)
            {
                if (active.category != 0 && active.category != skill.effectCond[i])
                    continue;

                switch ((SkillType)skill.effectType[i])
                {
                    case SkillType.Passive_CastBuff:
                        {
                            AddBuff(this, orderIdx, skill, i, 0);
                            break;
                        }
                    case SkillType.Passive_CastDebuff:
                        {
                            AddDebuff(this, orderIdx, skill, i, 0);
                            break;
                        }
                    default:
                        break;
                }
            }
        }
    }

   public override int RemoveDebuff(int count)
    {
        int cnt = base.RemoveDebuff(count);

        //107 맑아진 정신 - 디버프 해제 시 명중 버프
        if (HasSkill(107))
        {
            Skill s = SkillManager.GetSkill(classIdx, 148);
            turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), s.name, s.effectObject[0], cnt, s.effectRate[0], s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
            
            KeyValuePair<string, float[]> set = ItemManager.GetSetData(9);

            if(set.Value[1] > 0)
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, orderIdx), s.name, (int)Obj.AP, cnt, set.Value[1], 0, s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
        }

        return cnt;
    }

    void GetHeat(float amt)
    {
        //89 - 열기 : 강화, 98 - 열기 : 가속
        bool heatPower = HasSkill(89);
        bool heatSpeed = HasSkill(98);
        if (heatPower || heatSpeed)
        {
            currHeat = Mathf.RoundToInt(Mathf.Min(4, currHeat + amt));
            if (heatPower)
            {
                Skill s = SkillManager.GetSkill(classIdx, 89);
                float rate = s.effectRate[0] + ItemManager.GetSetData(7).Value[1];
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[0], currHeat, rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
            }
            if (heatSpeed)
            {
                Skill s = SkillManager.GetSkill(classIdx, 98);
                float rate = ItemManager.GetSetData(7).Value[1];  
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[0], currHeat, s.effectRate[0] + rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[1], currHeat, s.effectRate[1] + rate, s.effectCalc[1], s.effectTurn[1], s.effectDispel[1], s.effectVisible[1]));
            }

            //126 용광로 코어
            if (HasSkill(126) && currHeat >= 4)
            {
                Skill s = SkillManager.GetSkill(classIdx, 126);
                turnBuffs.Add(new Buff(BuffType.AP, LVL, new BuffOrder(this, -3), s.name, (int)Obj.AP, 1, s.effectRate[0], s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
            }

        }

    }
    void LoseHeat(float amt)
    {
        bool heatPower = HasSkill(89);
        bool heatSpeed = HasSkill(98);
        if (heatPower || heatSpeed)
        {
            currHeat = Mathf.RoundToInt(Mathf.Max(0, currHeat - amt));
            if (heatPower)
            {
                Skill s = SkillManager.GetSkill(classIdx, 89);
                float rate = s.effectRate[0] + ItemManager.GetSetData(7).Value[1];
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[0], currHeat, rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
            }
            if (heatSpeed)
            {
                Skill s = SkillManager.GetSkill(classIdx, 98);
                float rate = ItemManager.GetSetData(7).Value[1];  
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[0], currHeat, s.effectRate[0] + rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
                turnBuffs.Add(new Buff(BuffType.Stat, LVL, new BuffOrder(this, -1), s.name, s.effectObject[1], currHeat, s.effectRate[1] + rate, s.effectCalc[1], s.effectTurn[1], s.effectDispel[1], s.effectVisible[1]));
            }

            //126 용광로 코어
            if (HasSkill(126) && currHeat < 4)
                turnBuffs.buffs.RemoveAll(x => x.name == SkillManager.GetSkill(classIdx, 167).name);
        }
    }
}