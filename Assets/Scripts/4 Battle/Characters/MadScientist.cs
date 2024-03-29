﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MadScientist : Character
{
    ///<summary> 144 시선 집중 기계, 157 하이라이트 부츠, 162 골렘 주종 역전 </summary>
    public bool isMagnetic = false;
    ///<summary> 171 타임리프 </summary>
    public int turnCount = 0;
    
    ///<summary> 카오스 패닉 2세트 - 폭탄 AP 감소 시 사용 </summary>
    int bombState = 0;

    public override void OnBattleStart(BattleManager BM)
    {
        base.OnBattleStart(BM);
        //162 골렘 주종 역전
        if(HasSkill(162))
            isMagnetic = true;
    }
    public override void OnTurnStart()
    {
        base.OnTurnStart();
        bombState = 0;

        //144 시선 집중 기계
        string s1 = SkillManager.GetSkill(classIdx, 144).name;
        //157 하이라이트 부츠
        string s2 = SkillManager.GetSkill(classIdx, 157).name;
        
        //162 골렘 주종 역전
        isMagnetic = HasSkill(162) || turnBuffs.buffs.Any(x => x.name == s1 || x.name == s2);
    }
    public override void OnTurnEnd()
    {
        base.OnTurnEnd();
        //171 타임 리프
        if(HasSkill(171))
            turnCount++;
    }

    public override int GetSkillCost(Skill s)
    {
        float mul = 1;
        if(s.idx == 158 && bombState == 2 || s.idx == 159 && bombState == 1)
            mul = 0.5f;

        KeyValuePair<string, float[]> set = ItemManager.GetSetData(11);
        if (set.Value[0] > 0 && (s.idx == 135 || s.idx == 136 || s.idx == 137))
            mul = 2;
        if(set.Value[1] > 0 && (s.idx == 154 || s.idx == 155 || s.idx == 156))
            mul = 2;
        return Mathf.RoundToInt(base.GetSkillCost(s) * mul);
    }
    
    public override void ActiveSkill(int slotIdx, List<Unit> selects)
    {
        //적중 성공 여부
        isAcc = true;
        //크리티컬 성공 여부
        isCrit = false;


        //skillDB에서 스킬 불러오기
        Skill skill = SkillManager.GetSkill(classIdx, activeIdxs[slotIdx]);

        skillBuffs.Clear();
        skillDebuffs.Clear();

        if (skill == null) return;

        Passive_SkillCast(skill);

        //기초 과학자 4세트 - 순수 발명품 스킬 사용 시 버프
        KeyValuePair<string, float[]> set = ItemManager.GetSetData(12);
        if (set.Value[2] > 0 && skill.category == 1026)
        {
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.공격력, 1, set.Value[2], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.명중, 1, set.Value[2], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타율, 1, set.Value[2], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타피해, 1, set.Value[2], 1, -1));
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.방어력무시, 1, set.Value[2], 1, -1));
        }

        LogManager.instance.AddLog($"{name}(이)가 {skill.name}(을)를 시전했습니다.");
        //skill 효과 순차적으로 계산
        Active_Effect(skill, selects);
        SoundManager.Instance.PlaySFX(skill.sfx);

        //144 시선 집중 기계, 157 하이라이트 부츠 - 자석탱
        if (skill.idx == 144 || skill.idx == 157)
        {
            turnBuffs.Add(new Buff(BuffType.None, new BuffOrder(this, orderIdx), skill.name, (int)Obj.Magnetic, 0, 0, 0, 3, 1, 1));
            isMagnetic = true;
        }
        //148 업그레이드 파츠
        else if(skill.idx == 148)
        {
            Unit target = BM.GetEffectTarget(2)[0];

            target.AddBuff(this, orderIdx, skill, 0, 0);
            target.AddBuff(this, orderIdx, skill, 1, 0);
        }
        //165 업그레이드 세트
        else if(skill.idx == 165)
        {
            for (int i = 0; i < 3; i++)
            {
                Unit target = BM.GetEffectTarget(2)[0];

                target.AddBuff(this, orderIdx, skill, 0, 0);
                target.AddBuff(this, orderIdx++, skill, 1, 0);
            }
        }

        //카오스 패닉 2세트 - 유독류탄, 교란 폭탄 둘 중 하나 사용 시 이번 턴 동안 다른 스킬 AP 소모량 절반
        if (bombState == 0 && ItemManager.GetSetData(10).Value[0] > 0)
        {
            if (skill.idx == 158) bombState = 1;
            else if (skill.idx == 159) bombState = 2;
        }

        orderIdx++;
        buffStat[(int)Obj.currAP] -= GetSkillCost(skill);
        
        cooldowns[slotIdx] = skill.cooldown;
        //궁극의 피조물 2, 4 세트 - 컨트롤 스킬 쿨타임 감소
        set = ItemManager.GetSetData(11);
        if (set.Value[0] > 0 && (skill.idx == 135 || skill.idx == 136 || skill.idx == 137))
            cooldowns[slotIdx] = 0;
        if (set.Value[1] > 0 && (skill.idx == 154 || skill.idx == 155 || skill.idx == 156))
            cooldowns[slotIdx]--;

        StatUpdate_Turn();
    }
    protected override void Active_Effect(Skill skill, List<Unit> selects)
    {
        List<Unit> effectTargets;
        List<Unit> damaged = new List<Unit>();
        float stat;
        int count = skill.effectCount;

        //카오스 패닉 4세트 - 통제불능 스킬 히트 수 1 증가(기본적으로 늘어난 상태, 만족 안하면 1 빼줌)
        if (ItemManager.GetSetData(10).Value[1] <= 0 && (skill.idx == 130 || skill.idx == 139 || skill.idx == 149 || skill.idx == 164))
            count--;

        for (int i = 0; i < count; i++)
        {
            //152 피아 인식 장치
            if (skill.effectTarget[i] == 7 && HasSkill(152))
                effectTargets = GetEffectTarget(selects, damaged, 4);
            //160 세기말 방독면
            else if(skill.effectTarget[i] == 11 && HasSkill(160))
                effectTargets = GetEffectTarget(selects, damaged, 6);
            else
                effectTargets = GetEffectTarget(selects, damaged, skill.effectTarget[i]);

            switch ((EffectType)skill.effectType[i])
            {
                //데미지 - 스킬 버프 계산 후 
                case EffectType.Damage:
                    {
                        StatUpdate_Skill(skill);

                        float dmg = GetEffectStat(selects, skill.effectStat[i]) * skill.effectRate[i];

                        damaged.Clear();
                        foreach (Unit u in effectTargets)
                        {
                            if (!u.isActiveAndEnabled)
                                continue;

                            //172 상태 왜곡 장치, 카오스 패닉 5세트(골렘도)
                            if (HasSkill(172) && (u == this || (u.classIdx == 12 && ItemManager.GetSetData(10).Value[2] > 0)))
                            {
                                u.GetHeal(dmg);
                                damaged.Add(u);
                            }
                            else
                            {
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
                                    //크리티컬 연산 - dmg * CRB
                                    isCrit = Random.Range(0, 100) < buffStat[(int)Obj.치명타율];

                                    u.GetDamage(this, dmg, buffStat[(int)Obj.방어력무시], isCrit ? buffStat[(int)Obj.치명타피해] : 100);
                                    damaged.Add(u);

                                    Passive_SkillHit(skill);
                                }
                                else
                                {
                                    isAcc = false;
                                    LogManager.instance.AddLog($"{u.name}(이)가 스킬을 회피하였습니다.");
                                }
                            }
                        }

                        break;
                    }
                case EffectType.Heal:
                    {
                        float heal = GetEffectStat(selects, skill.effectStat[i]) * skill.effectRate[i];

                        foreach (Unit u in effectTargets)
                        {
                            //172 상태 왜곡 장치
                            if (HasSkill(172) && (u == this || (u.classIdx == 12 && ItemManager.GetSetData(10).Value[2] > 0)))
                                u.GetDamage(this, heal * skill.effectCalc[i] == 1 ? u.buffStat[(int)Obj.체력] : 1, buffStat[(int)Obj.방어력무시], 100);
                            else
                                u.GetHeal(skill.effectCalc[i] == 1 ? heal * u.buffStat[(int)Obj.체력] : heal);
                        }
                        break;
                    }
                case EffectType.Active_Buff:
                    {
                        stat = GetEffectStat(selects, skill.effectStat[i]);
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)
                            foreach (Unit u in effectTargets)
                                //172 상태 왜곡 장치
                                if (HasSkill(172) && (u == this || (u.classIdx == 12 && ItemManager.GetSetData(10).Value[2] > 0)))
                                    u.AddDebuff(this, orderIdx, skill, i, stat);
                                else
                                    u.AddBuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case EffectType.Active_Debuff:
                    {
                        stat = GetEffectStat(selects, skill.effectStat[i]);
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)
                            foreach (Unit u in effectTargets)
                                //172 상태 왜곡 장치
                                if (HasSkill(172) && (u == this || (u.classIdx == 12 && ItemManager.GetSetData(10).Value[2] > 0)))
                                    u.AddBuff(this, orderIdx, skill, i, stat);
                                else
                                    u.AddDebuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case EffectType.DoNothing:
                    break;
                case EffectType.CharSpecial1:
                    {
                        //골렘 조종 스킬
                        BM.GolemControl((int)skill.effectRate[i]);
                        break;
                    }
                default:
                    ActiveDefaultCase(skill, i, effectTargets, GetEffectStat(selects, skill.effectStat[i]));
                    break;
            }
        }
    }

    //궁극의 피조물 5세트 - 골렘이 처치 시 9LV 컨트롤 스킬 쿨 회복
    public void GolemKills()
    {
        for(int i = 0;i < activeIdxs.Length;i++)
            if(activeIdxs[i] == 168 || activeIdxs[i] == 169)
                cooldowns[i] = 0; 
    }

    protected override void Passive_BattleStart()
    {
        List<Unit> effectTargets;
        float rate = 1 + ItemManager.GetSetData(12).Value[0];

        //passive
        for (int j = 0; j < passiveIdxs.Length; j++)
        {
            Skill s = SkillManager.GetSkill(classIdx, passiveIdxs[j]);
            if (s == null)
                continue;

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

                //기초 과학자 2세트 - 1레벨 패시브 강화
                if ((s.idx == 132 || s.idx == 133 || s.idx == 134) && s.effectType[i] == (int)EffectType.Passive_EternalBuff)
                {
                    turnBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, s.name, s.effectObject[i], dungeonStat[s.effectStat[i]], s.effectRate[i] * rate, s.effectCalc[i], s.effectTurn[i], s.effectDispel[i], s.effectVisible[i]));
                    continue;
                }
                switch ((EffectType)s.effectType[i])
                {
                    case EffectType.Passive_HasSkillBuff:
                        {
                            if (HasSkill(s.effectCond[i], true))
                                foreach (Unit u in effectTargets)
                                    u.AddBuff(this, 0, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_HasSkillDebuff:
                        {
                            if (HasSkill(s.effectCond[i], true))
                                foreach (Unit u in effectTargets)
                                    u.AddDebuff(this, 0, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_EternalBuff:
                        {
                                foreach (Unit u in effectTargets)
                                    u.AddBuff(this, 0, s, i, 0);
                            break;
                        }
                    case EffectType.Passive_EternalDebuff:
                        {
                                foreach (Unit u in effectTargets)
                                    u.AddDebuff(this, 0, s, i, 0);
                            break;
                        }
                    default:
                        break;
                }
            }
        }
    }
}