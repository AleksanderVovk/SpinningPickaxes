using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PageManager : MonoBehaviour
{
    public static PageManager instance;

    public Page[] pages;

    private void Awake()
    {
        // SINGLETON
        instance = this;

        //StartCoroutine(TimerCorutine());
    }

    public static void OpenPage(Page page) => instance._OpenPage(page);
    private void _OpenPage(Page page)
    {
        _CloseAllPages();

        page.Open();
    }

    public static void OpenPageByName(string pageName) => instance._OpenPageByName(pageName);
    private void _OpenPageByName(string pageName)
    {
        _CloseAllPages();

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i].pageName == pageName)
            {
                pages[i].Open();
            }
        }
    }

    public static void ClosePageByName(string pageName) => instance._ClosePageByName(pageName);
    private void _ClosePageByName(string pageName)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i].pageName == pageName)
            {
                pages[i].Close();
            }
        }
    }

    public static void CloseAllPages() => instance._CloseAllPages();
    private void _CloseAllPages()
    {
        foreach (Page page in pages)
        {
            page.Close();
        }
    }
}
