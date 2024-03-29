﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MetalKnight : Character
{
    //63 신속 사격
    int resentSkillCategory = 0;
    float coolRate = 1;
    bool huntKill = false;
    public override void OnTurnStart()
    {
        base.OnTurnStart();
        resentSkillCategory = 0;
        guardBuffList.Clear();
    }

    public override int GetSkillCost(Skill s)
    {
        //63 신속 사격
        if (s.idx == 63 && resentSkillCategory == 1004)
            return 1;
        //엘리트 스나이퍼 2세트 - 목표 지정 스킬 AP 소모 0
        else if(s.idx == 54 && ItemManager.GetSetData(5).Value[0] > 0)
            return 0;
        else
            return base.GetSkillCost(s);
    }

    //44 빠르게 막기, 78 절대 방어
    public List<GuardBuff> guardBuffList = new List<GuardBuff>();

    //55 방어구 파괴
    Dictionary<Unit, int> armorBreakCount = new Dictionary<Unit, int>();
    
    public override void ActiveSkill(int slotIdx, List<Unit> selects)
    {
        //적중 성공 여부
        isAcc = true;
        //크리티컬 성공 여부
        isCrit = false;
        huntKill = false;

        //skillDB에서 스킬 불러오기
        Skill skill = SkillManager.GetSkill(classIdx, activeIdxs[slotIdx]);

        skillBuffs.Clear();
        skillDebuffs.Clear();

        if (skill == null) return;

        Passive_SkillCast(skill);

        //58 저격수
        if(skill.idx == 58 && selects[0].turnDebuffs.buffs.Any(x=>x.name == "표식"))
        {
            Skill tmp = SkillManager.GetSkill(2, 58);
            //엘리트 스나이퍼 3세트 - CRB 상승폭 증가
            float rate = tmp.effectRate[0] * (1 + ItemManager.GetSetData(5).Value[1]);
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, tmp.name, tmp.effectObject[0], buffStat[tmp.effectStat[0]], rate, tmp.effectCalc[0], tmp.effectTurn[0]));
        }
        //69 상처 벌리기
        if(skill.idx == 69 && selects[0].turnDebuffs.buffs.Any(x=>x.objectIdx.Any(y=>y == (int)Obj.출혈)))
        {
            AddBuff(this, orderIdx, skill, 0, 0);
            AddBuff(this, orderIdx, skill, 1, 0);
            foreach (Buff b in selects[0].turnDebuffs.buffs)
                for (int i = 0; i < b.count; i++)
                    if (b.objectIdx[i] == (int)Obj.출혈)
                    {
                        b.duration++;
                        break;
                    }
        }
        //79 현상금 사냥
        if(skill.idx == 79 && selects[0].turnDebuffs.buffs.Any(x=>x.name == "표식"))
        {
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타율, 1, 999, 0, -1, 0, 0));
            AddBuff(this, orderIdx, skill, 0, 0);
        }
        
        
        KeyValuePair<string, float[]> set = ItemManager.GetSetData(6);
        //오버파워 2세트 - 파괴적인 베기 CRC, CRB 상승
        if (skill.idx == 59 && set.Value[0] > 0)
        {
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타율, 1, set.Value[0], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타피해, 1, set.Value[0], 1, -1));
        }
        //오버파워 3세트 - 처형, 대구경 탄환 ATK, PEN 상승
        if((skill.idx == 52 || skill.idx == 71) && set.Value[1] > 0)
        {
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.공격력, 1, set.Value[1], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.방어력무시, 1, set.Value[1], 1, -1));
        }

        LogManager.instance.AddLog($"{name}(이)가 {skill.name}(을)를 시전했습니다.");
        //skill 효과 순차적으로 계산
        Active_Effect(skill, selects);
        SoundManager.Instance.PlaySFX(skill.sfx);

        //79 현상금 사냥
        if (skill.idx == 79 && selects[0].turnDebuffs.buffs.Any(x => x.name == "표식"))
            selects[0].turnDebuffs.buffs.RemoveAll(x => x.name == "표식");

        orderIdx++;
        if(!huntKill)
            buffStat[(int)Obj.currAP]-= GetSkillCost(skill);
        //엘리트 스나이퍼 2세트 - 목표 지정 AP 회복
        if(skill.idx == 54 && ItemManager.GetSetData(5).Value[0] > 0)
            buffStat[(int)Obj.currAP] = Mathf.Min(buffStat[(int)Obj.행동력], buffStat[(int)Obj.currAP] + 2);
        resentSkillCategory = skill.category;
        if(!huntKill)
            cooldowns[slotIdx] = Mathf.RoundToInt(coolRate * skill.cooldown);

        StatUpdate_Turn();
    }
    protected override void Active_Effect(Skill skill, List<Unit> selects)
    {
        List<Unit> effectTargets;
        List<Unit> damaged = new List<Unit>();

        for (int i = 0; i < skill.effectCount; i++)
        {
            effectTargets = GetEffectTarget(selects, damaged, skill.effectTarget[i]);

            switch ((EffectType)skill.effectType[i])
            {
                //데미지 - 스킬 버프 계산 후 
                case EffectType.Damage:
                    {
                        damaged.Clear();
                        StatUpdate_Skill(skill);

                        //skillEffectRate가 기본적으로 음수
                        float dmg = GetEffectStat(selects, skill.effectStat[i]) * skill.effectRate[i];

                        foreach(Unit u in effectTargets)
                        {
                            //명중 연산 - 최소 명중 20%
                            int acc = 20;
                            if (buffStat[(int)Obj.명중] >= u.buffStat[(int)Obj.회피])
                                acc = 6 * (buffStat[(int)Obj.명중] - u.buffStat[(int)Obj.회피]) / (u.LVL + 2);
                            else
                                acc = 6 * (buffStat[(int)Obj.명중] - u.buffStat[(int)Obj.회피]) / (LVL + 2);
                            
                            acc = Mathf.Max(20, acc);
                            //명중 시
                            if (Random.Range(0, 100) < acc)
                            {
                                isAcc = true;

                                isCrit = Random.Range(0, 100) < buffStat[(int)Obj.치명타율];

                                bool kill = u.GetDamage(this, dmg, buffStat[(int)Obj.방어력무시], isCrit ? buffStat[(int)Obj.치명타피해] : 100).Key;
                                damaged.Add(u);

                                //55 방어구 파괴, 73 난도질
                                if (HasSkill(55) || HasSkill(73))
                                {
                                    if (skill.category == 1004 || (HasSkill(83) && skill.category == 1005))
                                        if (armorBreakCount.ContainsKey(u))
                                        {
                                            armorBreakCount[u] += 1;
                                            if (HasSkill(55) && armorBreakCount[u] % 3 == 0)
                                            {
                                                u.AddDebuff(this, orderIdx, SkillManager.GetSkill(2, 55), 0, 0);
                                            }
                                            if (HasSkill(73) && armorBreakCount[u] % 5 == 0)
                                            {
                                                Skill s = SkillManager.GetSkill(2, 73);
                                                u.GetDamage(this, u.buffStat[(int)Obj.currHP] / (float)u.buffStat[(int)Obj.체력] * s.effectRate[0], 999, 100);
                                                u.AddDebuff(this, orderIdx, s, 1, 0);

                                                //오버파워 4세트 - 난도질 적용 시 버프 해제
                                                if(ItemManager.GetSetData(6).Value[2] > 0)
                                                    u.RemoveBuff(1);
                                            }
                                        }
                                        else
                                            armorBreakCount.Add(u, 1);
                                }
                                //57 자신감
                                if (kill && skill.category == 1005 && HasSkill(57))
                                    AddBuff(this, orderIdx, SkillManager.GetSkill(2, 57), 0, 0);

                                huntKill = skill.idx == 79 && kill && ItemManager.GetSetData(5).Value[2] > 0;
                                Passive_SkillHit(skill);
                            }
                            else
                            {
                                isAcc = false;
                                LogManager.instance.AddLog($"{u.name}(이)가 스킬을 회피하였습니다.");
                            }
                        }
                        
                        break;
                    }
                case EffectType.Active_Buff:
                    {
                        float stat = GetEffectStat(selects, skill.effectStat[i]);
                        if (skill.effectCond[i] == 0 || (skill.effectCond[i] == 1 && isAcc) || (skill.effectCond[i] == 2 && isCrit))
                            foreach (Unit u in effectTargets)
                                u.AddBuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case EffectType.DoNothing:
                    break;
                case EffectType.CharSpecial1:
                    {
                        //철벽 2세트 - 막기 버프 방어력 상승량 증가
                        float rate = skill.effectRate[0] * (1 + ItemManager.GetSetData(4).Value[0]);
                        //막기 버프 (44 빠르게 막기, 78 절대 방어)
                        guardBuffList.Add(new GuardBuff(skill.name, skill.effectTurn[i], skill.idx == 78, rate));
                        break;
                    }
                case EffectType.CharSpecial2:
                    {
                        //표식 부여
                        if(isAcc)
                        {
                            effectTargets[0].turnDebuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), "표식", (int)Obj.방어력, 1, 0.2f, 1, 2, 1, 1));
                            effectTargets[0].turnDebuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), "표식", (int)Obj.회피, 1, 0.2f, 1, 2, 1, 1));

                            //대상 무력화
                            if (HasSkill(82))
                            {
                                effectTargets[0].turnDebuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), "표식", (int)Obj.명중, 1, 0.15f, 1, 2, 1, 1));
                                effectTargets[0].turnDebuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), "표식", (int)Obj.치명타율, 1, 0.15f, 1, 2, 1, 1));
                                effectTargets[0].turnDebuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), "표식", (int)Obj.속도, 1, 0.15f, 1, 2, 1, 1));
                            }
                        }
                        break;
                    }
                default:
                    ActiveDefaultCase(skill, i, effectTargets, GetEffectStat(selects, skill.effectStat[i]));
                    break;
            }
        }
    }

    protected override void Passive_BattleStart()
    {
        List<Unit> effectTargets;

        for (int j = 0; j < passiveIdxs.Length; j++)
        {
            Skill s = SkillManager.GetSkill(classIdx, passiveIdxs[j]);
            if (s == null)
                continue;

            //75 균형잡힌 전투법
            if (s.idx == 75)
            {
                if (activeIdxs.Count(x => SkillManager.GetSkill(2, x).category == 1004) == 3 && activeIdxs.Count(x => SkillManager.GetSkill(2, x).category == 1005) == 3)
                    AddBuff(this, 0, s, 0, 0);
                continue;
            }
            //84 한길만을 걷다
            if (s.idx == 84)
            {
                if (activeIdxs.Count(x => SkillManager.GetSkill(2, x).category == 1004) + activeIdxs.Count(x => x == 0) == 6 ||
                    activeIdxs.Count(x => SkillManager.GetSkill(2, x).category == 1005) + activeIdxs.Count(x => x == 0) == 6)
                {
                    AddBuff(this, -2, s, 0, 0);
                    coolRate = 1 - s.effectRate[1];
                }
                continue;
            }

            for (int i = 0; i < s.effectCount; i++)
            {
                switch (s.effectTarget[i])
                {
                    case 0:
                        effectTargets = new List<Unit>();
                        effectTargets.Add(this);
                        break;
                    default:
                        effectTargets = BM.GetEffectTarget(s.effectTarget[i]);
                        break;
                }

                switch ((EffectType)s.effectType[i])
                {
                    case EffectType.Passive_HasSkillBuff:
                        {
                            if (HasSkill(s.effectCond[i], true))
                                foreach (Unit u in effectTargets)
                                    u.AddBuff(this, -2, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_HasSkillDebuff:
                        {
                            if (HasSkill(s.effectCond[i], true))
                                foreach (Unit u in effectTargets)
                                    u.AddDebuff(this, -2, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_EternalBuff:
                        {
                            foreach (Unit u in effectTargets)
                                u.AddBuff(this, -2, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_EternalDebuff:
                        {
                            foreach (Unit u in effectTargets)
                                u.AddDebuff(this, -2, s, i, 0);
                            break;
                        }
                    default:
                        break;
                }
            }
        }
    }
    protected override void Passive_SkillCast(Skill active)
    {
        for (int j = 0; j < passiveIdxs.Length; j++)
        {
            Skill skill = SkillManager.GetSkill(classIdx, passiveIdxs[j]);

            //65 칼 마무리
            if (skill.idx == 65 && active.category == 1004)
            {
                Skill tmp = SkillManager.GetSkill(classIdx, 65);
                skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", tmp.effectObject[0], activeIdxs.Count(x => SkillManager.GetSkill(classIdx, x).category == 1005), tmp.effectRate[0], tmp.effectCalc[0], tmp.effectTurn[0]));
                continue;
            }
            //67 총 마무리
            if (skill.idx == 67 && active.category == 1005)
            {
                Skill tmp = SkillManager.GetSkill(2, 67);
                skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", tmp.effectObject[0], activeIdxs.Count(x => SkillManager.GetSkill(2, x).category == 1004), tmp.effectRate[0], tmp.effectCalc[0], tmp.effectTurn[0]));
                continue;
            }

            for (int i = 0; i < skill.effectCount; i++)
            {
                if (active.category != 0 && active.category != skill.effectCond[i])
                    continue;

                switch ((EffectType)skill.effectType[i])
                {
                    case EffectType.Passive_CastBuff:
                        {
                            AddBuff(this, orderIdx, skill, i, 0);
                            break;
                        }
                    case EffectType.Passive_CastDebuff:
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
    protected override void Passive_SkillHit(Skill active)
    {
        for (int i = 0; i < passiveIdxs.Length; i++)
        {
            Skill s = SkillManager.GetSkill(classIdx, passiveIdxs[i]);

            //74 헤드샷 스트릭
            if (s.idx == 74)
            {
                if (active.category == 1005 && isCrit)
                {
                    float rate = s.effectRate[0] * (1 + ItemManager.GetSetData(5).Value[1]);
                    //엘리트 스나이퍼 3세트 - CRB 상승률 증가
                    turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), s.name, s.effectObject[0], buffStat[s.effectStat[0]], rate, s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
                }
                continue;
            }
            for (int j = 0; j < s.effectCount; j++)
            {
                switch ((EffectType)s.effectType[j])
                {
                    case EffectType.Passive_CritHitBuff:
                        AddBuff(this, orderIdx, s, j, 0);
                        break;
                    case EffectType.Passive_CritHitDebuff:
                        AddDebuff(this, orderIdx, s, j, 0);
                        break;
                }
            }

        }
    }

    protected override void StatUpdate_Turn()
    {
        float[] mulPivot = new float[13];
        float[] addPivot = new float[13];

        for (int i = 0; i < 13; i++)
        {
            mulPivot[i] = 1;
            addPivot[i] = 0;
        }

        turnBuffs.GetBuffRate(ref addPivot, ref mulPivot, true);
        turnDebuffs.GetBuffRate(ref addPivot, ref mulPivot, false);
        CalcGuardBuffPivot(ref mulPivot);

        for (int i = 0; i < 13; i++)
            if (i != 1 && i != 3)
                buffStat[i] = Mathf.CeilToInt(dungeonStat[i] * mulPivot[i] + addPivot[i]);

    }
    protected override void StatUpdate_Skill(Skill s)
    {
        float[] mulPivot = new float[13];
        float[] addPivot = new float[13];

        for (int i = 0; i < 13; i++)
        {
            mulPivot[i] = 1;
            addPivot[i] = 0;
        }

        turnBuffs.GetBuffRate(ref addPivot, ref mulPivot, true);
        turnDebuffs.GetBuffRate(ref addPivot, ref mulPivot, false);
        skillBuffs.GetBuffRate(ref addPivot, ref mulPivot, true);
        skillDebuffs.GetBuffRate(ref addPivot, ref mulPivot, false);
        CalcGuardBuffPivot(ref mulPivot);

        for (int i = 0; i < 13; i++)
            if (i != 1 && i != 3)
                buffStat[i] = Mathf.CeilToInt(dungeonStat[i] * mulPivot[i] + addPivot[i]);
    }
    void CalcGuardBuffPivot(ref float[] mul)
    {
        foreach(GuardBuff s in guardBuffList)
                mul[(int)Obj.방어력] += s.rate;
    }

    public override void AddDebuff(Unit caster, int order, Skill s, int effectIdx, float rate)
    {
        //철벽 5세트 - 절대 방어 중 디버프 무효
        if(ItemManager.GetSetData(4).Value[2] > 0 && caster != null && caster.classIdx == 10 && guardBuffList.Any(x=>x.name == SkillManager.GetSkill(2, 78).name))
            return;
        base.AddDebuff(caster, order, s, effectIdx, rate);
    }

    public override KeyValuePair<bool, int> GetDamage(Unit caster, float dmg, int pen, int crb)
    {
        KeyValuePair<bool, int> killed = base.GetDamage(caster, dmg, pen, crb);
        bool isUpdate = false;
        
         if (guardBuffList.Count > 0 || turnBuffs.buffs.Any(x=>x.name == SkillManager.GetSkill(classIdx, 61).name))
        {
            //56 카운터 어택
            if (HasSkill(56))
                caster.AddDebuff(this, orderIdx, SkillManager.GetSkill(2, 56), 0, 0);
            //81 가드 아드레날린
            if (HasSkill(81))
            {
                Skill tmp = SkillManager.GetSkill(2, 81);
                AddBuff(this, orderIdx, tmp, 0, 0);
            
                KeyValuePair<string, float[]> set = ItemManager.GetSetData(4);

                //철벽 4세트 - 가드 아드레날린이 공격력도 올려줌
                if(set.Value[1] > 0)
                    turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this, orderIdx), set.Key, (int)Obj.공격력, 1, set.Value[1], 1, 2, 1, 1));
            }
        }

        if (guardBuffList.Any(x => x.isReturn))
            caster.GetDamage(this, Mathf.Max(1, 0.2f * dmg), 100, 100);

        for (int i = 0; i < guardBuffList.Count; i++)
        {
            guardBuffList[i].shieldCount--;
            if (guardBuffList[i].shieldCount <= 0)
            {
                guardBuffList.RemoveAt(i);
                i--;

                isUpdate = true;
            }
        }

        if(isUpdate)
            StatUpdate_Turn();

        return killed;
    }

}
