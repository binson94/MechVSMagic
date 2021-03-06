using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ElementalController : Character
{
    int resentCategory = 0;
    bool[] elementalUsed = new bool[3];
    int usedAP = 0;

    public override void OnBattleStart(BattleManager BM)
    {
        base.OnBattleStart(BM);
        usedAP = 0;
    }

    public override void OnTurnStart()
    {
        base.OnTurnStart();
        for (int i = 0; i < 3; i++)
            elementalUsed[i] = false;
    }

    public override int GetSkillCost(Skill s)
    {
        float rate = 1;
        if(s.category == 1007 || s.category == 1008 || s.category == 1009)
            rate -= ItemManager.GetSetData(15).Value[1];
        return Mathf.RoundToInt(base.GetSkillCost(s) * rate);
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

        if (skill == null)
        {
            Debug.LogError("skill is null");
            return;
        }

        
        //200 불안정한 마법
        if (skill.idx == 200)
        {
            //불 -> 무조건 치명
            if (resentCategory == 1007)
                skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.치명타율, 1, 999, 0, -1));
            //물 -> 무조건 명중
            else if (resentCategory == 1008)
                skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.명중률, 1, 999, 0, -1));
        }
        //208 응축된 조화
        if(skill.idx == 208)
        {
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.공격력, usedAP, 1, 0, -1));
        }

        KeyValuePair<string, float[]> set = ItemManager.GetSetData(15);
        //삼위 일체 2세트 - 원소 스킬 ACC 상승
        if(set.Value[0] > 0 && (skill.category == 1007 || skill.category == 1008 || skill.category == 1009))
            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)Obj.명중률, 1, set.Value[0], 1, -1));
        //삼위일체 4세트 - 불 CRC상승,ACC감소, 물ACC상승,ATK감소, 바람ATK상승,CRC감소
        if(set.Value[2] > 0 && (skill.category == 1007 || skill.category == 1008 || skill.category == 1009))
        {
            Obj up = skill.category == 1007 ? Obj.치명타율 : skill.category == 1008 ? Obj.명중률 : Obj.공격력;
            Obj down = skill.category == 1007 ? Obj.명중률 : skill.category == 1008 ? Obj.공격력 : Obj.치명타율;

            skillBuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)up, 1, set.Value[2], 1, -1));
            skillDebuffs.Add(new Buff(BuffType.Stat, BuffOrder.Default, "", (int)down, 1, set.Value[2], 1, -1));
        }

        Passive_SkillCast(skill);

        LogManager.instance.AddLog($"{name}(이)가 {skill.name}(을)를 시전했습니다.");
        //skill 효과 순차적으로 계산
        Active_Effect(skill, selects);
        SoundManager.instance.PlaySFX(skill.sfx);

        if (skill.category == 1007)
            elementalUsed[0] = true;
        else if (skill.category == 1008)
            elementalUsed[1] = true;
        else if (skill.category == 1009)
            elementalUsed[2] = true;

        if (1007 <= skill.category && skill.category <= 1009)
            usedAP += GetSkillCost(skill);

        int tmp = resentCategory;
        resentCategory = skill.category;

        orderIdx++;

        //200 불안정한 마법 - 바람 -> AP 소모 반환
        if (skill.idx != 200 || tmp != 1009)
            buffStat[(int)Obj.currAP] -= GetSkillCost(skill);

        //진정한 지배자 5세트 - 응축된 조화 사용 시 전투간 CRC, CRB 버프
        set = ItemManager.GetSetData(14);
        if(skill.idx == 208 && set.Value[2] > 0)
        {
            turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this), set.Key, (int)Obj.치명타율, 1, set.Value[2], 1, 99, 0, 1));
            turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this), set.Key, (int)Obj.치명타피해, 1, set.Value[2], 1, 99, 0, 1));
        }
        

        //211 통달의 경지 - 다음 다중원소 스킬 AP 소모량 감소
        if (HasSkill(211) && skill.category == 1011)
        {
            Skill s = SkillManager.GetSkill(5, 211);
            if (turnBuffs.buffs.Any(x => x.name == s.name))
            {
                turnBuffs.buffs.RemoveAll(x => x.type == BuffType.AP && x.isDispel);
                
                //진정한 지배자 4세트 - 통달의 경지 발동 시 무작위 적 디버프 해제
                if(ItemManager.GetSetData(14).Value[1] > 0)
                    GetEffectTarget(selects, selects, 4)[0].RemoveBuff(1);            
            }

            turnBuffs.Add(new Buff(BuffType.AP, new BuffOrder(this, orderIdx), s.name, (int)Obj.APCost, 1, s.effectRate[0], s.effectCalc[0], s.effectTurn[0], s.effectDispel[0], s.effectVisible[0]));
        }

        //212 정령왕의 계약 - 정령 소환 스킬 쿨타임 감소
        if (HasSkill(124) && ((94 <= skill.idx && skill.idx <= 96) || (109 <= skill.idx && skill.idx <= 111)))
        {
            //정령의 대리인 4세트 - 정령왕의 계약 쿨감 100%로 상승
            float rate = ItemManager.GetSetData(13).Value[1] > 0 ? 0 : 0.5f;
            cooldowns[slotIdx] = Mathf.RoundToInt(skill.cooldown * rate);
        }
        else
            cooldowns[slotIdx] = skill.cooldown;

        CountSkill();
        StatUpdate_Turn();
    }
    protected override void Active_Effect(Skill skill, List<Unit> selects)
    {
        List<Unit> effectTargets;
        List<Unit> damaged = new List<Unit>();
        float stat = 0;

        for (int i = 0; i < skill.effectCount; i++)
        {
            effectTargets = GetEffectTarget(selects, damaged, skill.effectTarget[i]);
            stat = GetEffectStat(effectTargets, skill.effectStat[i]);

            switch ((EffectType)skill.effectType[i])
            {
                //데미지 - 스킬 버프 계산 후 
                case EffectType.Damage:
                    {
                        StatUpdate_Skill(skill);

                        float dmg = stat * skill.effectRate[i];

                        //193 원소 결합
                        if (skill.idx == 193)
                            dmg = dmg * 0.5f * (1 + elementalUsed.Count(x => x));

                        damaged.Clear();
                        foreach (Unit u in effectTargets)
                        {
                            if (!u.isActiveAndEnabled)
                                continue;

                            //명중 연산 - 최소 명중률 10%
                            int acc = 20;
                            if (buffStat[(int)Obj.명중률] >= u.buffStat[(int)Obj.회피율])
                                acc = 60 + 6 * (buffStat[(int)Obj.명중률] - u.buffStat[(int)Obj.회피율]) / (u.LVL + 2);
                            else
                                acc = Mathf.Max(acc, 60 + 6 * (buffStat[(int)Obj.명중률] - u.buffStat[(int)Obj.회피율]) / (LVL + 2));
                            //명중 시
                            if (Random.Range(0, 100) < acc)
                            {
                                isAcc = true;
                                //크리티컬 연산 - dmg * CRB
                                isCrit = Random.Range(0, 100) < buffStat[(int)Obj.치명타율];

                                bool kill = u.GetDamage(this, dmg, buffStat[(int)Obj.방어력무시], isCrit ? buffStat[(int)Obj.치명타피해] : 100).Key;
                                damaged.Add(u);

                                if (kill)
                                    OnKill();
                                if (isCrit)
                                    OnCrit();

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
                case EffectType.Heal:
                    {
                        float heal = stat * skill.effectRate[i];

                        foreach (Unit u in effectTargets)
                            u.GetHeal(skill.effectCalc[i] == 1 ? heal * u.buffStat[(int)Obj.체력] : heal);
                        break;
                    }
                case EffectType.Active_Buff:
                    {
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)
                            foreach (Unit u in effectTargets)
                                u.AddBuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case EffectType.Active_Debuff:
                    {
                        if (skill.effectCond[i] == 0 || skill.effectCond[i] == 1 && isAcc || skill.effectCond[i] == 2 && isCrit)

                            foreach (Unit u in effectTargets)
                                u.AddDebuff(this, orderIdx, skill, i, stat);
                        break;
                    }
                case EffectType.Active_RemoveBuff:
                    {
                        foreach (Unit u in effectTargets)
                            u.RemoveBuff(Mathf.RoundToInt(skill.effectRate[i]));
                        break;
                    }
                case EffectType.Active_RemoveDebuff:
                    {
                        foreach (Unit u in effectTargets)
                            u.RemoveDebuff(Mathf.RoundToInt(skill.effectRate[i]));
                        break;
                    }
                case EffectType.CharSpecial1:
                    {
                        BM.SummonElemental(this, skill.category);
                        break;
                    }
                case EffectType.CharSpecial2:
                    {
                        BM.UpgradeElemental(this, skill.category);
                        break;
                    }
                case EffectType.CharSpecial3:
                    {
                        int type = BM.SacrificeElemental(this, skill);
                        switch (type)
                        {
                            //불 - 적 하나 즉사(보스는 데미지)
                            case 1007:
                                {
                                    Unit target = BM.GetEffectTarget(4)[0];

                                    if (target.IsBoss())
                                    {
                                        float dmg = buffStat[skill.effectStat[1]] * skill.effectRate[1];

                                        int acc = 20;
                                        if (buffStat[(int)Obj.명중률] >= target.buffStat[(int)Obj.회피율])
                                            acc = 60 + 6 * (buffStat[(int)Obj.명중률] - target.buffStat[(int)Obj.회피율]) / (target.LVL + 2);
                                        else
                                            acc = Mathf.Max(acc, 60 + 6 * (buffStat[(int)Obj.명중률] - target.buffStat[(int)Obj.회피율]) / (LVL + 2));
                                        //명중 시
                                        if (Random.Range(0, 100) < acc)
                                        {
                                            //크리티컬 연산 - dmg * CRB

                                            isCrit = Random.Range(0, 100) < buffStat[(int)Obj.치명타율];

                                            target.GetDamage(this, dmg, buffStat[(int)Obj.방어력무시], isCrit ? buffStat[(int)Obj.치명타피해] : 100);

                                            Passive_SkillHit(skill);
                                        }
                                        else
                                        {
                                            LogManager.instance.AddLog($"{target.name}(이)가 스킬을 회피하였습니다.");
                                            isAcc = false;
                                        }

                                        break;
                                    }
                                    else
                                    {
                                        target.GetDamage(this, 9999, 100, 100);
                                    }
                                    break;
                                }
                            //물 - 2턴 피해 면역
                            case 1008:
                                {
                                    turnBuffs.Add(new Buff(BuffType.None, new BuffOrder(this, orderIdx), skill.name, 0, 0, 0, 0, 2, 1, 1));
                                    break;
                                }
                            //바람 - 적 전체 데미지, 맞은 적 TP 0으로
                            case 1009:
                                {
                                    List<Unit> targets = BM.GetEffectTarget(6);

                                    float dmg = buffStat[skill.effectStat[2]] * skill.effectRate[2];

                                    foreach (Unit u in targets)
                                    {
                                        if (!u.isActiveAndEnabled)
                                            continue;

                                        int acc = 20;
                                        if (buffStat[(int)Obj.명중률] >= u.buffStat[(int)Obj.회피율])
                                            acc = 60 + 6 * (buffStat[(int)Obj.명중률] - u.buffStat[(int)Obj.회피율]) / (u.LVL + 2);
                                        else
                                            acc = Mathf.Max(acc, 60 + 6 * (buffStat[(int)Obj.명중률] - u.buffStat[(int)Obj.회피율]) / (LVL + 2));
                                        //명중 시
                                        if (Random.Range(0, 100) < acc)
                                        {
                                            isCrit = Random.Range(0, 100) < buffStat[(int)Obj.치명타율];

                                            u.GetDamage(this, dmg, buffStat[(int)Obj.방어력무시], isCrit ? buffStat[(int)Obj.치명타피해] : 100);
                                            damaged.Add(u);

                                            Passive_SkillHit(skill);
                                        }
                                        else
                                        {
                                            LogManager.instance.AddLog($"{u.name}(이)가 스킬을 회피하였습니다.");
                                            isAcc = false;
                                        }
                                    }

                                    BM.Sacrifice_TP(damaged);

                                    break;
                                }
                        }

                        //정령의 대리인 4세트 - 정령 희생 시 모든 디버프 해제
                        if (ItemManager.GetSetData(13).Value[1] > 0)
                            RemoveDebuff(turnDebuffs.Count);

                        break;
                    }
                default:
                    break;
            }
        }
    }

    protected override void Passive_BattleStart()
    {
        List<Unit> effectTargets;
        KeyValuePair<string, float[]> set = ItemManager.GetSetData(14);

        for (int j = 0; j < 3; j++)
        {
            Skill s = SkillManager.GetSkill(classIdx, passiveIdxs[j]);
            if (s == null)
                continue;

            //210 순수한 원소
            if (s.idx == 210)
            {
                if (activeIdxs.Count(x => SkillManager.GetSkill(5, activeIdxs[x]).category == 1007) + activeIdxs.Count(x => x == 0) == 6 ||
                    activeIdxs.Count(x => SkillManager.GetSkill(5, activeIdxs[x]).category == 1008) + activeIdxs.Count(x => x == 0) == 6 ||
                    activeIdxs.Count(x => SkillManager.GetSkill(5, activeIdxs[x]).category == 1009) + activeIdxs.Count(x => x == 0) == 6)
                    AddBuff(this, -2, s, 0, 0);

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
                                    //진정한 지배자 2세트 - 원소 활용 강화
                                    if (s.idx == 196 && set.Value[0] > 0)
                                        u.turnBuffs.Add(new Buff(BuffType.Stat, new BuffOrder(this), s.name, s.effectObject[i], s.effectStat[i], s.effectRate[i] * (1 + set.Value[0]), s.effectCalc[i], s.effectTurn[i], s.effectDispel[i], s.effectVisible[i]));
                                    else
                                        u.AddBuff(this, -1, s, i, 0);
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
    public override KeyValuePair<bool, int> GetDamage(Unit caster, float dmg, int pen, int crb)
    {
        //209 숭고한 폭발 - 바람 정령 희생 시 2턴 피해 면역
        if (turnBuffs.buffs.Any(x => x.name == SkillManager.GetSkill(classIdx, 209).name))
        {
            LogManager.instance.AddLog("숭고한 폭발 효과로 피해를 받지 않습니다.");
            return new KeyValuePair<bool, int>(false, 0);
        }

        return base.GetDamage(caster, dmg, pen, crb);
    }
}
