using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared large background artwork for the Tackle Store and Island / Fish Index.
/// Uses the supplied 754x59 wave image as a normal stretched UI image.
/// </summary>
public static class LargeShopBackdropTheme
{
    private const float Alpha = 0.84f;
    private static Sprite cachedSprite;

    public static void Apply(Image target)
    {
        if (target == null) return;
        Sprite sprite = GetSprite();
        if (sprite == null) return;
        target.sprite = sprite;
        target.type = Image.Type.Simple;
        target.preserveAspect = false;
        target.fillCenter = true;
        target.color = new Color(1f, 1f, 1f, Alpha);
    }

    public static void RestoreDefault(Image target, Color color)
    {
        if (target == null) return;
        target.sprite = null;
        target.type = Image.Type.Simple;
        target.preserveAspect = false;
        target.color = color;
    }

    public static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(EncodedImage);
        }
        catch (FormatException exception)
        {
            Debug.LogError("Backdrop image data is invalid: " + exception.Message);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "LargeShopBackdrop";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        if (!texture.LoadImage(bytes, true))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Backdrop image could not be decoded by Unity.");
            return null;
        }

        cachedSprite = UnityEngine.Sprite.Create(
            texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        return cachedSprite;
    }

    private const string EncodedImage =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/" +
        "2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wgARCAA7AvIDASIAAhEBAxEB/8QA" +
        "GwAAAgMBAQEAAAAAAAAAAAAAAAEDBAUCBgf/xAAbAQEBAAMBAQEAAAAAAAAAAAABAAIDBAUGB//aAAwDAQACEAMQAAAB+dx1+fvfzu1zWWOVkqobaqKbRURl" +
        "bKgVsqKbTqFXCoquFMq4UyrhTGuOmRcKhVwpiXHSdXXSaXCm0tFUq2VCrZUGtOqVbdQS2VBLhUGtlQq4qaa6UibpSVXus9xf5pFXXTaXeqLi93nNx0XnvLG+" +
        "Z6rR7zm46Pea3DXvYEmfP62fxXTye5ueAz8dX0Xn55s417Pwly+r6Hb8pBt5/dw+H5z1+rpedi19e5UyY9XZpGSa903MC19E/MPJlOq6MrBXQ2Cu5nK4U6hK" +
        "mIVM7gEnIHU5CRMQjTEJUzgdTEJEzhGmcDSZwOpnA0mIhJlC6mcDiZwvIlIhpVG2kcREhwq7OCeyMbs4BkIXU3ULsZiISVxdJKRCSOIqdwWnDi0sxw9x57vL" +
        "y5J7WXHz9vr/ACd6maY3Hr6erjiKXZog606m8oQWamjs44rxcvXaI6uOVwomGeioVt1yqEMpVGDIoyZHERKoyZHEVIcESkY0hGRKRjSOIqQ4KkIxJHE4kIxp" +
        "XE0kcZEqjKlcLaRxCSuJxMRmRKRtOzkTtxtuzgSQ4KkI+kaOK76ivOMGlNH2cdq5m7HRwwZPuNk8746fVsDk9Dw/OhV1etDLpY5TVYpOToWriW7H2viPYeP3" +
        "+dHo7vTlWj1vYdXk5feV4no172Viw+f9JtwZL0dOlDVnGrFpR69tE6NO5DRJNSDKBlJopi6hHSpDGTCkwoGQmxhMhDKTBm00GnAAwNQMdJps2hH1y3Fg0TBm" +
        "JoxOhpJ33xYywjU2x06KMs1XfzvZs0+vi0KmBdwy0I4dbfqzrtZa709XE5y5M7P9t1z9vgIvqGtpx+R6/wBiz9Xm+VsZvkd3VqWMP3j1SYGl4vp0V84i+d+r" +
        "j7dnXth5kjTlp45dkbTo6Msf/8QAJhAAAgIDAAIBBAMBAQAAAAAAABEBAgMEExASBRQVICEiMDJQBv/aAAgBAQABBQLqdDodDodDqdTodDodDqdTodTqdDqd" +
        "TqdTqdTqdTodDqdTodTqdDodDodDodDqdDodDodDqdTqdDodTsdZOp1k6nU6nY7HWTqdDodTqRmO0nY7HaTtJ2k7SdiuYx7CKb3qfdLSU3WfdaYYn5a2Sb73" +
        "vXJvonekpuMxWsvrsWMt8wi/yt7Ft60ltmS2zJ9TIxjGMYxjGMYxjGMYxjGMYxjGMYxjGMYxjGMfhjGMYxjGMYxjGMYxjGMYxjGMYxjPYix7k3IyyTlUZM0l" +
        "dyak7cWpexiw3yldmmAts2vPWToTlJyk5CbnuMYxnsex7DGMYxjGMYxjGMYxjGMYxjGMYxjGMYxjGPwxjGMY/L8sYxjGMYxj8sYzDFZyZM1cxljnk9zHMPct" +
        "rRTK639iLFcHIy55uRDGibk5TsdjpJ2PY9j2GewxjGMYxjGMYxjGMYxjGMYxjGMYx+GMYxjGMYxjGP8AtY/wfhjGYK1vk2aVxTOT96XyX0VsuxbNdInIp1s+" +
        "Kmbbzxnz0pOW1KV1C2WbGLVyZT7bnL6GQto5ILa14L/xKSr5beGMZ7DGMYxjGMYx+WMYxjGMY/D8sY/DGMYxjGPwxj/tiHNo9ZrS15pozJTRxH23XmPsdrmz" +
        "o59T8qyjNntkFJLPj8OO+T5G2lmLfxt7GppZNy1+erGvq5N3Jh+Cw6kZd6uKmf5P2J3JJ3LH1DOnsWxUuW15qVp7HIY/DH/YxjH5f9T8vwx/ix+H+L/Bj8si" +
        "VOLXnJPWuKJz2kxze84sGe0444xg39dZ/jvitydj4LHjjLrRWa617Tkxfxy+0FbqbZWe7Na8QattTFh55drNr/CzSNjfcaHxs78zbDoYtv5CYM+7bNM3Pc6H" +
        "uMrIyyk5z/0n+UeIgmCKTYxYPQvlZTHbJOHUpUtt0wk7l7HWZLZD3MW9fEYs2PdjZ0MHrm1tiC1bwTW0nFlPjNnKYP8Aze0YPgtLDGXawaNdj5Sc06mtO/OT" +
        "LyrsbPqZtrsWnxWs2PU/UHtAzoP/ALVfFIc1iIjJ/nFWLZawjcvNKVIKf6qbNYiNasWjLaYNfLfIVEYjHBT9RufuPkslseOn7PWPekc42Z/W9P4Vj9W8IjxH" +
        "j//EACsRAAICAgIBAgUDBQAAAAAAAAABAhEDEgQTISJBBRAVMWEUI0IyQFGh4f/aAAgBAwEBPwFQRojRHWjRGiOtGiOtHWjRGiNEaI60aI0RojRGiNEaI0Ro" +
        "jRGiOtHWjrR1I60aI60daOpHUjrR1InhUh8CD80fTIZH5JfDY42qRHiYoK2T4vZ9lQvhuP38kOFBfZEeMkdC/wAGpqampqampqampRqampRRqampRRqUUUUU" +
        "UUUUUUUUUS9KsS7Y+DDx545tV6R49vCOPjlilp5f5JyjDx7lJevILLjl/Ig8b9xY1VigmaGpqUUUUUUUUUUUUUUUUUUUUUUUUUUV8pPXyyU8kv6fBllOP3mf" +
        "UORjd1ujF8Uw5PEk4v8AKI5ItXZCSyChr4SMsZPG0jhLTaHml7sny9/Rx/P59v8Apn5iw/t4/MzBw83Le+d2Y+HCKpIXHivY11+wskvc2/P963RPLoXt65Dy" +
        "yyvWPg/TQgSqHsNQnC2hceUPXCVIjzHHw0S+LPH/AA/2fXc+R6RSRi48+UuzNNtHIcePBRxKmcPApvyQgo+EOVC+VL5f/8QAJhEAAgMBAAICAAYDAAAAAAAA" +
        "AAECERIDITEEExAiMkFCYSBAUf/aAAgBAgEBPwHTNGjTNm2aZpmmbZpmmaZpm2bZo0aNGmaZpmmaZpmmaZtm2bZo0bZtm2bZpimfYdOsaIdov0S7ybI9q9j7" +
        "Nj7MfVn2v/po0aNFllllllllllllllllllllll/4WWWWWWJ2Slj2T7Kj7K9nSakiKbF58RPqml4RKPRfsOT9Dk0bLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLIp" +
        "z8IXPnH9XkjHnL+J04JK4i4yY4STomnz8jlryyDjryd68V7IcK/N0I8XN0vQ5c/jL+yfy5M++T/c1r2PnH9jP9f7li8kOez9P5In1x5LT8i7SmRuZclLI3G6" +
        "aJfFT9MfwL/kQ+HHXlknDg8wXk4KXaT2/B8jp9UfBPo5eyMbHSLNP8P/xAAxEAABAwEFBgUEAgMAAAAAAAABAAIRIQMSIjFREDJBUmFxEyAjYGIEMFCBQpGC" +
        "0fH/2gAIAQEABj8C9nSVSiqVTEVUrRG7/azlZqXYG9VmXlYWgLeWZWftYTFNl7dZzFek3FzuUkkn2m29lKDLJgDB/GERsAyTG2DZpiJz8l61z5P9qv8Az2mA" +
        "8w3jCF2bp5lRXwwF/XJFxqSZ2tc+SziE5wF1vAINFTos71rry7MDC7sFUBvcreat4LKeyqIQmoQynp7AhQBKxOuqrnFbzwvStWu6Gi9SzLevDzShJvRQbb1o" +
        "cA4aprbGy8MahEabMAw8XHII2dhXmtNVcsxKDrU+M/TgEWjABoKLD5NVp2VKo1iNVn+evPoFDQs1hkqoA7rE+6rto+8Oqlj/AAX/ABopY42g+JVHEdwuEIVv" +
        "LJaLPYWkwnu+odWKAcUfDaXEnJq8T6x1OQLwrIXLIcApJuWXErw7EBgR0R0Wf2N4/nKKXVKosIWPGVAjs1Z3QtVuxs5gsTQ7vmvTcbM/2qQ8dFiY4fpZSFuH" +
        "9BYPp7U/4qba7Yj5Gqm2c62OmQRuQ34sCxZaK9uWQzOqDG0aOAUkwunscpoOSgIQY8sxVSa7DedMeaeKwmNjR1Qa2gGweQ/Y/8QAKBAAAwABBAEEAQUBAQAA" +
        "AAAAAAERIRAxUWFBIHGBkTBAUKGx0eHw/9oACAEBAAE/IW/I3LL5L5LKGxZZZZXJXJZRRZRZXJXJRRXJRZZRRZXJRfJZZZRfJZRZTE5fJfJXJXJRfJfJRXJ3" +
        "F86Vll8lcljd5KL5L5ExXIxeTuKK5O47jsFzCfkcvIx62b4+B47T0Ob+RizoajBwh2LDkS6NexTXQY+Q0rlyYi/jthD/AOUZPJF7G8u+Rocw7tDD9LX+x4Zg" +
        "C/Av6QACYvVOtetajgORY3yyyIbunOJJhESxJLco3GWmaW87fxyxfKcOv4WyGtleWzkYz2DOSo0jS/23j8+FBccX8ACl0UvpBesBRPUQQjZ5ew18FUeK49mN" +
        "W8sfshvccwbszKSzN1RKkV2JEi+O3/zobqtlElhJwkMyY0UqKXkfUdcEvBD6Pf6C/wBO87gFfiQC6F6gF6wFKUpdKUpSl1KUui+gOeeRuHKqpq8h8cDEZLF/" +
        "0HO1mfZhrID48GcFYVkpHnONCJW/CQyqTg7ezvv65HMQ9yfuDFJfzPhU3kfB/eEbRrdoS+iR4exdYU1X+fQAUv4wAuil9Av4gBfQKUpSl9bFJbsa9penSyTd" +
        "GWV0WWV/UNIWKd90yn4lo2z+t+2xNGymRj5ZQ9rZMnhCrlR9mTJXPheRApay5t0zDdoOGbfU4YNaneG79Vwi+LrebL3YuU1Xx/2J7+FIWtM5ycse8j86+h7T" +
        "w7FW1jNMl63GbCVHsal1KXSl0pSlL6QpdFKUpSlKXRS6LqUpSi0LopSl0UpdSlGGoa3RWHR5Zit8HCvYla+FkwReXg+yU6QhmfdZI9rkwP4/wcvcKf1BwThe" +
        "CvL2C4SyzX/Q2JNOie3WRAGG7AdTJMUrMbcGa5mlUlMkzTb3f+GDOFCUxc3y9IiMft9saOn7hIc/svuGqIfIgmfRTdjVeCPbI6BSlKUutL+OlKUpSlKUpdKX" +
        "00pSlKUul0UpSlIuTzg5MEFyhjFrE/8AAIZg2Cep34M/7TZCHGfEP8e0Od82M8t2QnfkapN9T3IXrF/sWeoPAbueS3+6OJLv9gtsMfAfbzcGWqbt+iFbg87f" +
        "Wf5FQt+J9/8ATctpt4l2zO3ojbn4tggcDkvaVXfyWGxzjMLWJBoVxQb+MipR4Misnpnrn4IQms1hPxz0LRaQn4EwISnsTSURiSgKz2EKkRLwilVOOaNptCpI" +
        "fVjkhhRXZsjnsMFYrYuiH4FSFU8PAiu+QoVvdozu5GxhYaCEjbJIZTvgc2lwxnkiEIHINDAaMNP/2gAMAwEAAgADAAAAENg5yROwoVrjnNQuTqi97jRrv/il" +
        "ULs8fTHqBZZR2y9ZmAC2Pek7RSClgpSA9OYH4qAitHg5SGL98tejAwstMBILDtQNoiB4z7SclN2LMVPwrolP/ccU6pV1u8+vUitZ2UuzjYItWbBmUSMMjNhV" +
        "VLGcuIItsE87ALiNY6LL/wDHRrQ7SMH7xgGEbKa8WKJLAz3/xAApEQACAQMDAwMEAwAAAAAAAAAAAREhMWEQQVFxkaEggfAwscHRQOHx/9oACAEDAQE/EEth" +
        "cQuIXAYjEYDEYDAYjEYjEYDEYjEYjEYh8BiMRiMRhMBgHwCXsPiHwDdsYTAPjHwlEgZykKCsLQZaL1+eENa0zftt79idK/Hgs6jatEiiv5uhGN8X6domNsVy" +
        "lMpBIrt1b3/3JCCUEiJlzNvlILQlrL5ZfFI6lU+F+h9EPav2LEozAhTLUERBet4/oIb9Pn6iHoJCa2F5LPh5K/C+v4HAOgTT7pR4ITgCDzYsSWRLCIKph4EP" +
        "FxCMfLXDd6N3zwSWhMr7HTl4GrTPdvbL/CVsE8P2LoraOCGgnW8Diju0aCgnSmk601nShQnSRtFNHpQoUIktCLESxVakCu6D79x1TUvlk/cVzlwOpaNr/wBe" +
        "BdDfShOR8PYcK171b808GI8bdrL2RMALPhfsV9zuL4YREoM2pY0rkFVp/8QAJREAAwACAgIBAwUAAAAAAAAAAAERIWEQMUFRoSAwcUCBsdHh/9oACAECAQE/" +
        "EH7hv7L9m4r2bDcbjcbDcbjcbjYbC/Zfsv2X7NxuF7jcbuLcbjYbDYJ/ZfsSPJsNwvYImLI1bjQhDQh6KEIh65V8In0x7zwn+hD1yuJcpfSClLwQXIxGNEIe" +
        "FlJ2ecWq7Oj6EbBnmnYOJtMx7E7wf3eAC+wCILgXIQgYxjWd6r4EmP5nxhOhU/OCcKmUCbZkk9kYkX8ENRYV4Xl/0KSIJxKh3aPuBLitFnW/bgvNKX6aXii5" +
        "r4pRMTKVlKYZPMxediGscNE5TSMJ0iDLFBPI8lzfkc1q/ORCV/D/AEbSzR2NvLGpRPkoSM73ZTkoiRQmYZT/xAAmEAEAAgEEAgMAAwEBAQAAAAABABEhMUFR" +
        "YRBxgZGhILHRwfDh/9oACAEBAAE/EKuqb1lJhiLCcV7zvnLi7rHljzM75TujN3sOVg+87oHvFN4H4bthyzvQ5Wd8OVlO7O9l9ZgO6HOxeO+d8V3gG8DIeWA7" +
        "x55e1SD3Yc6d6dqHKnaxfdKYB3zMOqPiIDdjzzvhMcqI7xQy+4KawiD0jlwNgbVFN0tNX7ne+5i1Q5EOZBb/ALjZWuC1h2LhO2hpbWVI+FZfmXj2ppEihd0R" +
        "m7g1BK9g62xDRO61PohGt+XSDbnoiSNkfI6NWDUkbj/7y5A+VaX99DGXcAs2z2wd5vzG/X9x7+HhZ3jbxab+F/g74vb98S/8Sy8MoVl9paFYxeEFIeH2nt43" +
        "mnX+EvswUIzj8IQLxfG+Bz8M5eWN4Kot1hl5JOG8xdYPmJLxXzFuEjJfv8ymhthl/sC3thkUjhasSDnvTAID15gQCze9ZWuqpPZd3tle38Qal89L8bugv41m" +
        "FW1N+v8Aq9zWhApf2KGX4YPP3H28AXdbCGczu/Y5xXeNN4w+B7R8PvH+A3guZhvMt/AtCSsPM0w7T3hfeEe0vz5TtMN5aXYeVa4dp7w7+X5S8tCK7spHCa5j" +
        "PaPeMWJ+Z7w8WwS8O0I9495m6wa7hGO8RbjBgAzUtmviHVgAaNbrqvbmMFYxagahKnl6lLYQrbuFMWRXl4cHRLpUg3FMvTr3HuJzCYtexR2tT0z63rqSpjhB" +
        "gOiOWpe01pUvNMXbu8EPZPaxxp9pctP3KdN+0Suj7Iyw9yI7xjDee3hXmPeatZ7T2h3j5H7iGA0uaNZiawJvPeZ7x7TthhPeY+c8kj3muLzL8z2loSWJwTCJ" +
        "dHwyntcLTF8+Tcc4rBTRGLwkkBPaXYOGFzVxGcGGuDN4nHqgGgXRe7pHzyFDg8gWfBBIkVdwhaQFtkq2lvq5sH+K024hQyvEc3Kh2XGRNbITg2v5mprA3XoL" +
        "u7vbFL9T2sFqYy1tw9/xgmRzqsuup81O8QJQPRf0WxlAjlftQxfUbD+oUtA4H8ibgaiP3K3VNYvq6guoZaoY9GkVczDwMMe/iz7x7wn3leY94dp7RtvD3h4s" +
        "vNrgWU0uMZQErPaDmMMvMG4U8KXDvK1Kep7XCCCcIXlGPjPMygksljMUy8Re4Rd4DaX1hnAPshFf2EMBTs1f0fsWBr/04YWOeJ/qiD1iFim6NT7SIcdFY9G/" +
        "1LXccQDSNWWEERSyzWD7DIJtBkq9gug21ZqBwCmHEvbGb1NvSm17HL65sZqw9wTaif8ArIgdmLfJNEMxEf7XN3ejMfEKkrn/AOLrykqRa3gPKYCXT4Svtb/K" +
        "vUQGyABB0RBtver/AJBLUbRThIyuh5/zBF5uAv8AJhD5Vj6lr8AvpM/FYtS5Cg1vMbuHPn/I33jD47y/jfc5TCL5V+BWEEYPjnPf+DZKwt4DrzFITj5f1CNv" +
        "lPE0ebOF5SHmlZ7+BAY0nBFzpLMXL4FzQr/AhQxWpo+XecMcYQ8SdhUOHYw+i38g041cH2sUHDQBjsCOroO4/cr8TlZQQ/T6mPcWuP8A3qXYIb3X8Q1/a2py" +
        "GBtWYircsv6iu8kOiMIMHG7cTdiDDuxNC6VcSYuocMO+dDeFw/Kst62OYJJosMGgf1X2hkE6YT2bHW+8yBHGZ/sP9QgQco2u6brz/wAmMKLW0EIFmbOvZhB+" +
        "gmmK/Ea2PpAdGIGfaEgW/wAZftEO8xuxw1ilQHFwfMcY1l5eEDcX4ljwuDHws0ly5cu5byEHkWIOvE8A5mEtL+YMuXLTpC0O0wYUhbwHmY7wgvp/Hkjs9xaZ" +
        "o+46X+EUmzk4h1vURsqHIf8Ai2Wtbem8sRBq8D2wW54Kj9ZfyJz4QK91giYuNjmvf+TXt3sf2Hb6Mx/kJzY6jgB3cPT/ALEwoKQgOtR8MS/NX4ef1lDE7Seq" +
        "MD+VVUVtBq2qUIh3H/IKJzof0oH7AM4qq6t/UheHn8GtBK9oJoMYf3k9y6hLFg99vcOe3hO/fl2+ozl0Bgev+sIeHVMrkFwfskEOb62iixUBAtTiOa+UhWQX" +
        "uB1S5gQXSboOY7PgpxAMkQuUcQDGIDiVKKuUSsyiASkrMQGVKlEogD9SkpKVpEDaAO0pnEQJRUpxKcSsQCVAJRAlFSoEArSU4gFwImfCoLiFylaSnEqJKJRR" +
        "DRjtBAuRtACtOkEj4ggsGMTAUBslwYOMBQTWCYwa9xNzShsHRhoACoBIuj+oAMNHL8mpddNFSklGzSCwuXrRrFCiogWXGhxbQSdjBuqX9BbNP3qTWbvywfqY" +
        "nIvMElwGCb/N0WabFL1qGrqVcXOSEQUURhC+ZR1LlwxAaMQ10mgEon//2Q==";
}
