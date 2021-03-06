﻿using UnityEngine;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;
using System.Text.RegularExpressions;
using TMPro;

/// <summary>
/// This script is able to fill in the text's text gradually, giving the effect of someone typing or fading in the content over time.
/// </summary>

public class TypewriterEffect : MonoBehaviour
{

    public string val;

    /// <summary>
    /// 每秒打印字符数
    /// </summary>
    public int charsPerSecond = 40;

    /// <summary>
    /// 空格延迟时间
    /// </summary>
    public float delayOnSpace = 1f;

    /// <summary>
    /// 打字机效果完成时触发事件委托
    /// </summary>
    public UnityEvent onFinished = new UnityEvent();

    private Tweener doFadeTweener;
    TextMeshProUGUI mLabel;

    string mFullText = "";
    int mCurrentOffset = 0;
    float mNextChar = 0f;
    bool mReset = false;
    bool mActive = false;
    bool mSupportRichText;

    /// <summary>
    /// 打字机效果当前是否处于活动状态
    /// </summary>
    public bool isActive { get { return mActive; } }

    /// <summary>
    /// 是否打印完成
    /// </summary>
    public bool isComplete = true;

    /// <summary>
    /// 调用打字机方法
    /// </summary>
    /// <param name="content">显示的字符</param>
    /// <param name="supportRichText">是否需要富文本</param>
    public void WordByWord(string content,bool supportRichText)
    {
        QuickShow();
        isComplete = false;
        val = content;
        mReset = true;
        mActive = true;
        mSupportRichText = supportRichText;
    }

    //在逐渐显示的时候点击对话，快速显示所有文字
    //重复QuickShow不会重复触发onFinish
    public void QuickShow()
    {
        //结束淡入淡出
        if (doFadeTweener != null)
            doFadeTweener.Kill(true);
        else
        {//结束WordByWord
            mReset = false;
            Finish();
        }
    }

    /// <summary>
    /// 完成打字机操作并立即显示所有文本
    /// </summary>
    public void Finish()
    {
        if (mActive)
        {
            mActive = false;

            if (!mReset)
            {
                mCurrentOffset = mFullText.Length;
                mLabel.text = val;
            }

            onFinished.Invoke();
        }
    }

    private void Awake()
    {
        //onFinished.AddListener(() => Debug.LogError("Finish!"));

        mLabel = GetComponent<TextMeshProUGUI>();//可替换为Text类型
        mLabel.text = "";
        onFinished.AddListener(() => isComplete = true);
    }

    Regex tagPattern = new Regex("<[^>]*>[^<]*</[^>]*>");
    Regex contPattern = new Regex(">.*<");
    private StringBuilder sb;
    private Dictionary<int, cont> realPosList = new Dictionary<int, cont>();
    class cont
    {
        public int realIdx;
        public bool bCont;
        public string endStr;
        public cont(int realIdx, bool bCont = false, string endStr = "</color>")
        {
            this.realIdx = realIdx;
            this.bCont = bCont;
            this.endStr = endStr;
        }
    }
    cont GetRealIndex(int index)
    {
        cont value;
        if (realPosList.TryGetValue(index, out value))
        {
            return value;
        }
        return new cont(index);
    }
    string ReplaceCharInTag(string lastText)
    {
        sb = new StringBuilder();
        realPosList.Clear();
        var group = tagPattern.Matches(lastText);
        var startIndex = 0;
        if (group.Count > 0)
        {
            for (int i = 0; i < group.Count; i++)
            {
                var m1 = group[i];

                for (int j = startIndex; j < m1.Index; j++)
                {
                    realPosList.Add(sb.Length, new cont(j));
                    sb.Append(lastText[j]);
                }
                startIndex = m1.Index + m1.Length;

                var m2 = contPattern.Match(m1.Value);
                if (m2.Success)
                {
                    for (int k = 1; k < m2.Value.Length - 1; k++)
                    {
                        realPosList.Add(sb.Length, new cont(m1.Index + m2.Index + k, true));
                        sb.Append(m2.Value[k]);
                    }
                }
                if (i == group.Count - 1)
                {
                    for (int j = startIndex; j < lastText.Length; j++)
                    {
                        realPosList.Add(sb.Length, new cont(j));
                        sb.Append(lastText[j]);
                    }
                }
            }
        }
        else
        {
            return lastText;
        }
        return sb.ToString();
    }
    void Update()
    {
        if (!mActive) return;

        if (mReset)
        {
            mNextChar = 0;
            mLabel.text = "";
            mCurrentOffset = 0;
            mReset = false;
            if (mSupportRichText)
            {
                mFullText = ReplaceCharInTag(val);
            }
            else
            {
                mFullText = val;
            }
        }

        while (mCurrentOffset < mFullText.Length && mNextChar <= Time.unscaledTime)
        {
            int lastOffset = mCurrentOffset;
            charsPerSecond = Mathf.Max(1, charsPerSecond);

            // Automatically skip all symbols
            while (ParseSymbol(mFullText, ref mCurrentOffset)) { }
            ++mCurrentOffset;

            // Periods and end-of-line characters should pause for a longer time.
            float delay = 1f / charsPerSecond;
            char c = (lastOffset < mFullText.Length) ? mFullText[lastOffset] : '\n';


            if (c == ' ')
                delay += delayOnSpace;

            if (mNextChar == 0f)
            {
                mNextChar = Time.unscaledTime + delay;
            }
            else mNextChar += delay;

            if (mSupportRichText)
            {
                cont ct = GetRealIndex(mCurrentOffset - 1);
                mLabel.text = val.Substring(0, ct.realIdx + 1) + (ct.bCont ? ct.endStr : "");
            }
            else
            {
                mLabel.text = mFullText.Substring(0, mCurrentOffset); ;
            }
        }

        if (mCurrentOffset == mFullText.Length)
        {
            onFinished.Invoke();
            mActive = false;
        }
    }


    /// <summary>
    /// Parse an embedded symbol, such as [FFAA00] (set color) or [-] (undo color change). Returns whether the index was adjusted.
    /// </summary>
    static public bool ParseSymbol(string text, ref int index)
    {
        int length = text.Length;
        if (index + 17 > length || text[index] != '<' || text[index + 16] != '>') return false;
        if (text[index + 1] == '/') return false;
        if (!text.Substring(index, 8).Equals("<color=#")) return false;

        string alpha = text.Substring(index + 14, 2);
        int a = (HexToDecimal(alpha[0]) << 4) | HexToDecimal(alpha[1]);
        index += 17;
        return true;
    }

    static public int HexToDecimal(char ch)
    {
        switch (ch)
        {
            case '0': return 0x0;
            case '1': return 0x1;
            case '2': return 0x2;
            case '3': return 0x3;
            case '4': return 0x4;
            case '5': return 0x5;
            case '6': return 0x6;
            case '7': return 0x7;
            case '8': return 0x8;
            case '9': return 0x9;
            case 'a':
            case 'A': return 0xA;
            case 'b':
            case 'B': return 0xB;
            case 'c':
            case 'C': return 0xC;
            case 'd':
            case 'D': return 0xD;
            case 'e':
            case 'E': return 0xE;
            case 'f':
            case 'F': return 0xF;
        }
        return 0xF;
    }
}
