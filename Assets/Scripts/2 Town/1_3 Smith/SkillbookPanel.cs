using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillbookPanel : MonoBehaviour, ITownPanel
{
    [SerializeField] SmithPanel SP;
    ///<summary> 스킬 학습 시 필요한 재화 아이콘(최대 2종류) </summary>
    [SerializeField] Image[] resourceImages;
    ///<summary> 스킬 학습 시 필요한 재화 갯수(최대 2종류) </summary>
    [SerializeField] Text[] resourceTxts;
    ///<summary> 분해 시 획득하는 재화 갯수 </summary>
    [SerializeField] Text[] disassembleTxts;
    ///<summary> 학습 여부 표시 텍스트 </summary>
    [SerializeField] GameObject learnedTxt;
    ///<summary> 선행 스킬 표시 텍스트 </summary>
    [SerializeField] Text reqSkillTxt;

    ///<summary> 학습 버튼, 학습 불가 시 투명도 설정 </summary>
    [SerializeField] Image learnBtn;
    ///<summary> 학습 버튼 텍스트, 학습 불가 시 투명도 설정 </summary>
    [SerializeField] Text learnTxt;

    bool canLearn;

    public void ResetAllState()
    {
        canLearn = true;
        //재화 정보 불러오기
        LoadResourceInfo();
        //선행 스킬 정보 불러오기
        LoadReqSkillInfo();


        bool learned = GameManager.Instance.slotData.itemData.IsLearned(SP.SelectedSkillbook.Value.idx);
        canLearn &= !learned;

        Color color = canLearn ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, 0.5f);
        learnBtn.color = color;
        learnTxt.color = color;
        learnedTxt.SetActive(learned);
    }

    ///<summary> 스킬 학습 시 필요한 재화 정보 불러오기 </summary>
    void LoadResourceInfo()
    {
        List<Triplet<int, int, int>> resources = ItemManager.GetRequireResources(SP.SelectedSkillbook.Value);

        //필요 재화 정보 불러오기
        int i;
        for (i = 0; i < resources.Count; i++)
        {
            resourceImages[i].sprite = SpriteGetter.instance.GetResourceIcon(resources[i].first);
            resourceImages[i].gameObject.SetActive(true);
            resourceTxts[i].text = $"({resources[i].second} / {resources[i].third})";
            if (resources[i].second < resources[i].third)
            {
                resourceTxts[i].text = $"<color=#f93f3d>{resourceTxts[i].text}</color>";
                canLearn = false;
            }

            disassembleTxts[i].text = $"+{Mathf.CeilToInt(resources[i].third / 10f)}";
        }
        for (; i < 2; i++)
        {
            resourceImages[i].gameObject.SetActive(false);
            resourceTxts[i].text = string.Empty;
            disassembleTxts[i].text = string.Empty;
        }
    }
    ///<summary> 선행 스킬 정보 불러오기 </summary>
    void LoadReqSkillInfo()
    {
        Skill skill = SkillManager.GetSkill(GameManager.SlotClass, SP.SelectedSkillbook.Value.idx);
        reqSkillTxt.text = string.Empty;

        if (skill.reqskills[0] != 0)
        {
            if (GameManager.Instance.slotData.itemData.learnedSkills.Contains(skill.reqskills[0]))
                reqSkillTxt.text = $"{SkillManager.GetSkill(GameManager.SlotClass, skill.reqskills[0]).name}";
            else
            {
                canLearn = false;
                reqSkillTxt.text = $"{reqSkillTxt.text}<color=#ed2929>{SkillManager.GetSkill(GameManager.SlotClass, skill.reqskills[0]).name}</color>";
            }

            for (int i = 1; i < 3 && skill.reqskills[i] > 0; i++)
                if (GameManager.Instance.slotData.itemData.learnedSkills.Contains(skill.reqskills[i]))
                    reqSkillTxt.text = $"{reqSkillTxt.text}\n{SkillManager.GetSkill(GameManager.SlotClass, skill.reqskills[i]).name}";
                else
                {
                    canLearn = false;
                    reqSkillTxt.text = $"{reqSkillTxt.text}\n<color=#ed2929>{SkillManager.GetSkill(GameManager.SlotClass, skill.reqskills[i]).name}</color>";
                }
        }
        else
            reqSkillTxt.text = "없음";
    }

    ///<summary> 스킬 학습 버튼 </summary>
    public void Btn_SkillLearn()
    {
        if (!canLearn) return;

        ItemManager.SkillLearn(SP.SelectedSkillbook);
        SP.ResetSelectInfo();
    }
    ///<summary> 스킬북 분해 버튼 </summary>
    public void Btn_SkillbookDisassemble()
    {
        ItemManager.DisassembleSkillBook(SP.SelectedSkillbook);
        SP.ResetSelectInfo();
    }
}
