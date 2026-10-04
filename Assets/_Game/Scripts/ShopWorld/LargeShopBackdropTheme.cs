using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared large background artwork for the Tackle Store and Island / Fish Index.
/// This is a pre-cleaned transparent PNG of the user's rounded wave panel.
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
            bytes = Convert.FromBase64String(EncodedPng);
        }
        catch (FormatException exception)
        {
            Debug.LogError("Backdrop PNG data is invalid: " + exception.Message);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "RoundedShopIndexBackdrop";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        if (!texture.LoadImage(bytes, false))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Backdrop PNG could not be decoded by Unity.");
            return null;
        }

        cachedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f),
            100f);

        return cachedSprite;
    }

    // SHA-256 of decoded PNG bytes:
    // 27807dd179041537b504d227f409739ce9498b3effe0661d0647435084103f5f
    private const string EncodedPng =
        "iVBORw0KGgoAAAANSUhEUgAAApYAAAFMCAMAAABGYQzsAAAAwFBMVEUcbY8eottHpcUhT16k9fVq1uJOm60ShawiNDnR/v4jPEQCat8BPaBDfI07x9lFcHaItraJu8oBJFcAK2cANHUAR5EgICABWKkAOoYAHUgBZbQBd8sBUpoBl+0BhdYBaccBiecFpvQASqcIt/ccJiwBcroJx/oO1vwBQXwN9/wox/cjt/YL5/0BWsYGldYtiK42k7UCeeZt+/0bRVEgISJF2usr1vYw+PopZXYaMzpP5/ZO+fsaOkUlWGks5/uO/Px3p8/qAAAAQHRSTlP/////////////////////////////AP////////////////////////////////////////////////////////////////15ytDAAANXdJREFUeNrtnQljm7jWsONm6+3M3Pt9xhQCGGwT2xk3cSeZjNNJWuf//6tXK0gggdiFo5MuSWz2x2fT0dHZVyNGtJMzcwuMGCyNGDFYGulOnuFfJLe3z0QyLxssPwwMz7eUBTX5VyA/FeRvVs7Pz78A+QzkC5GfX/79l/z8k+4X/Jge5DMn56l8+fz8XBtLePnJTnPX9G8XktwStRtXfjd1k/O/z7PyixXhLxn5IZYzXq6RfP/+l1SuPsnlikj6m0tWLt7f3y+AsL/jtqMbs9sAQRslcvW/80I2z2RM/nv+4+qSPf4ncKzJ5L///e8OywH8obJrIGDrg7L8oyDwZP9pV5jjvv7zSuR4BP/k3voq3Unm3a+tytPbUxfyTuVCLO+FwrzxPxkBXF2df76thOXz7fkf/x8eFJzY29vbyxHIPZAtlA0jcUaWqbDf8xLLZZORbXNJzpsXfu/89+UieW+sdmmyUy3fS+ER2APJ9y58+XA48vKSyBsUCa1P6MW3F5G8MRsiQnkoL+A/Vz9kGlOE5e3zH3AnF/CwmMjkuvL4LectijrASg9nUwEb0UdL4WO1rCwqxy26KYJbvpyLj6B01SJc4Wf5TyIsZfRbTBv++U+xsHgiMjn9SjToxdW5GEwBls/nV4jod3hYhCS9uNw9WczDdmWBRBXbJf+a6Anmtli29xGaS3a2QH/BFxXZHkTbimROd7EoEfGNEp+m5IOS8smgKSaOEnlPJUt08vaMwmVBFSvMPJbPvy4wlU/Ae7qXMYlvQ54rP/QLJAy5H8oBLWK0gSyaSMHOKu646O2PRAQv+f6jn7+Pi7DsFNMfinRxAidA7E8FSX2kVN9m0EzVbaJzEahIbV59flbA8vl/UL1Ct/L18jJxLe8Frk+ZXmrfqC/rmUzRuWUNZc2zkrwiAaLOp0b6acp+fAs+OMIDFF0dJlOuL1kqMRdb1lnIIZ3h8o2x7k/vV19Ksbz9fIWt/xNA8h3C+QZ05pFCWUhk1afbF5ntf1zkOxIeJWOMpfwU6eYExaz5kbLZwBJgMrHmu0fo5RQgMtpQUwriDfAD/3ZBRJSEVW+X71c/b4uxvP1yhcPv10uIJpDX18PhIIx2ahvQPfgKF9juQNNDzM+8JYPdrf7u2WFgtaPvF/tHSnAiPytkfShO+yZwIi6xJaf6kEEN/Qq/hIgAf/f7/Xy+h/+CH5Exv+eg5ImGem67Pd7DV9+eLv++LcLy+ecnGH6/Hg9PCMrL19d/DocdZbLqA4YnWS/wCdvyKpuEOYta9rEbMkuYFDCKPX0fs5bTspm3cpulXBInM7XMWwgmhhG/RkMO8KD3+HHvKdFbDPFLLlq/x6DHVKm+HAGXzwVY3v7AOSFA5RPM0UImdxkmMzEnvmn0K2w/MJ8vhLFuM2SSgGMuVii88pAdR5lbJR0mpSb9IQLidy3wqlOrE7OmGUc0m6wTN+cePOYSuAD3CYRMFjmJjrCWhfwCc/70/fZWiuXtz4t34I1uj1BPvr0eUihlDlN94Ao+4kVh+aIM/0UakarSXPEyiravpwlDJV0IkPQ8L/IiKj76W405FbApl+JIKKYGm/52TlQ5vIr1mnKJqERIytPHFPZ74GDeFWB5Bg34/fYNqkoMJaMos0yGFbRjLQvUYiKUhZqHuNlelcEtSARJAUqQwwR6iUSppJiqIMnswmM2SfcQUSyZSAtdEeMRzUmuGqEJCIUPd43EWyMsw/1ksiFEJgBKhg3gQAAw5cenq3+fJVg+/3x/fQFBNwjdgXd52HLGO0OkGL6wdXtyMqJOJcatVJi3RULJ7tPzVHaaDZ5odJTqT/QrQuZuB7xJwKPnrfEOsLqcAJ+RNfVz6gUIh6iAar0/vv+6lWB5++vpeATuA4jeUfAtUJQFSHbn6siPFLbLrk93Tz9klbYlZ1Rwd9LnHZKEudjKeo0lyrKqvik+zzm9H8mdDmlyi3kw+3gJsUTbnaE/kMvJ5riJJ3uIzZ6Yez4wSSkio2Xx9nj5hxTLq3+O2ziGaUrkAeSCnH07RjpR+pwMqmXV9+yrbF+6Z2loHbWDZUaJVtpsvYfqKGv7wI9YVYbk6UGLjRMta8xkAL7A1tfxYTuhUTlhj+EoMbacFo63r1dyLIGOXG6OxHxnPUp5sqcaf0XSva4Nc5qwXQWefVP1iARD5GDx2paSPa698PXtAHXSnBgjn/oDC+xVokdK3o3vIvzpDFO5Xk92u0nIB+Y5W5uJrwDr+/jw+lmC5fnlYbNcHg7IfGOPoKmilOFHXRHuhqAPYPd+QLs79v3CjKAcPmGQksOSwOm0RKl8J/SF9dPb8YB9QHxGnGXfg2hmD59dEMD3o19DSAPIZXB2to4Pk+trmrD3iZmWMunD543SqpPN67kEyz92G2jmgcaMec90Ucv8KcGYZTOrOrvhdDBJwxGS7sngGUm4zCBam0gKtwO1pmDn6/Xh8HaIkUe4DBMqIZc4xHn9B8begEooiEsfPdQAqsuz/W6yv07NXibQS3BZ875sCDwaoC6vnsVYXu7ieQizpYzxXtT0xLJAKjs2DMJdGfbhmJQ7gTy2RVjWVJyOkngAj80SmcY9xDLiI/Td0xsIvcEjSbgkEnhQV05219fX9HnTCC8HTu7qoW4Nl7tLGZZAWQIsgQEnTNYLDOoSWQLq2KFUCE/U1CWnOZGv6KhDKdot5Av/43lzNI6IGJyHXJrT89d7oEmXIXoaEMqzIBXwnutvEwwlThetZdTknzD0g+Yx51ymWD5fAirnUFnCMeR8Jqg6k6366miPTE5u7SNSI+ZrxFhSncSFzo6y0Pcq2G8vxyTRfOjVOYnAQxjgojcz+/m2hAkh+FhDaLUZLM8Cb/IN2e8UNQk1ySmlMRg8IlCXEix3yzCMN0hX5qPuKkBihrwhZO01j+yZq6hprQUfkahKUkdqeik/An+TwU7ZdAecvoOqFzxniNQepoh8JhLCmSPEAcIS+ZIMlmeTb/A1ChqlMn9vc6EXipxCz99PxFh+3s0TLJuYbk8jqQ4nAVIUgqntSTTKUjcXKYIykKApdDwVsLRth9khcBshUzFMEa05HQz+DUm+MlyHAS/r3eQ6XGPoEJVsGgLatTVyIjkqUywjjOUXIZY/49Bf72IY7VRMpaz1ZJJhUzUmqXQBwt2ypRRVx214psSqTdmql3GZIGWzu/WQQxlODm+7icc4jtjY4lGcPQjGWSbPgFu5xw8fWHYY7aTsCC8yyFwK2AAe81yI5d8ESxJ+q2DYrTfZolTKZFcAsyDYTsBU3W2OKWFoAv6rwGW57bbRDgO62wDeq3UYv77twNYcljB9hEz55hBfIzVr25Bp8JtdeA3AWgcIy3U6ZYu/cLo7m0gCJsLS34uxPF8SLAtKrTLokTEo3aHExrhmHFJe2iBLAXEZymqpG9ErqYIpV5pFepKDEoEJMzyQS7BbOwBqKwTxxzfPS5QacjvByx7G8vAan1G6oAGfwKQReC1AunKdQhmxQNqM3N3dzWYzvDnc/zoEzO9/CbH8tQSPaUeUpZhJb8yShiPJVzsDz3IPUmlQuiy65nDKWEBZCCTbLQMkZgLI5PLb3gevQd2HdRhgC4fZNoYSYom8Wu8aVmJ8m5BXkbYEbiWuIkLxOGtAUiYxiiyVEEtMpg2PF8KUqRTLyPu2JMPxOS3ZAShNU8S6SEFNRE0kRWQGWck7aSX2O8hYUQSiM7lcAqwc5pes04nDPxIdQYt9vaapIfTea1jaBqgMYL2G48mgJDAmMksEvAI2A3sWY/kMsISpqXm2uKVN85wkgEsSxJ43LkjrVOooYelJ7S+XcJQHO+keskgSLj004WcdsFTaRJsiDqGVxFY4sHMHv47314wvlKWSOSCDIlScMxZM5zdwoFCI5e2vSQ7Ldj1GOhpbRTzHO2VRiVzkXMq2YEYvvUw2KMel7eACtdzvCaFn15/+2e32XsBo0kShnk3ia5T8cbKeNj4Dfm+QS8zm5CzgufTsAiyjDJbr/m5/lQGNj4SlyJBjtSW8JfB7rLjRdgJdS213ih7M+HxbY3DyYE7e31838X4P33AXcFb+bP1tskdqy2HrOULq9IEj5rmEAhPxtquK5R5jGbYd3shywupZuJPEUzXtzb2coIV/jPwFnTlLhgVR1tp3gMIkYHrstiKV6PiTSQj+d1Nzi/xABOB+9/Z23ExC4FJmjP/ZGQl2cKlGAiWZ/IhrM33PQbqVo3KGf+asOPgjxdJLsGyBSmFdgNgvaoDniWBZSKbHKD74kbZXNNtHJnQnjYN2S1Rm4XuRJ4p9xFhmDTuNSuD3HpzCGE+u13AYnN/mLISD1WiEEkbgwAfd7/eTZUznNqZTJDCbbqoes1je2Z4LgqciLOcNsJRV8RX46ypO/Iew7KWW3COsEfEFDdpgP4DDZonHpgWqGPqWQi5FVEIwg8l+D7Uwyley7iegcr+bhPswiXaAK4CmO96TDiz8NAk/cjCGSGPeQS5nLJbBzA3WYizPIZaTOfINCtFLIubURKh4SZWxrAHoyXqZwEZGfjqa7Ie5lgmkbxBsMBCHa+G+ZVhybiXK3lAsYTkRDMQ9mPJmtCoIdg44MeThCoL1Pt7e03nhMddgB02mg0wBMl3KIaDSsjgs7bVUWzrOZB5Kyl1qWNgUq6CRnLiFV8tIoAeLjbmPQRSQGeNOQfebPazVzVZuJlzlrfkd+uPSTA7OLYJwCKd5IJfrxIoDioJ4d02qidYwg7Tf3JPWQmhuIt8Ij2AJxxuYSCelkmJ5XoJl1FIUjYAKWhTNwEyzrD1kKXDaJ9GUqRZ6ZLiMaQcrCKafgMnWCyFd5wUZ+w1dSTwUCbGkGe9gjcd3AJMAPZrbDM7OvsXXZA4MKmHb3P+ZUImgFEyGx9kjz2GtdxLzBOCvBMvzBEs231BEHRrhD5wCYIIOpGreswVwikHqHsqAHghg9pg8aqZTAddnDfekOiIwIwIz3IlNjfjk0yR0sk7l9eV+7QWpd4nUmL9GtzCE/V2+BYlbeYhDUlULoISakjRfRaHOfJGbBL9gptVFeTDdalgKtB+X3hlG2jTmSkyVjut59dFXMzpJ8RmFkj5mJxn49B8fFxRL3JTq5RiTuJweiIAIi9eAGuRMOXAWJ3AGY0C8S4IlDnbWyw0gbk+wXH/bxQQSQOX15Ji2FiRQYiIzPT6YEdogA6aNsfyigKWT05SBXtIcTQVwFStzuhxgYC/Yo1RSr5Fv7hJS75K0lXx7oY0o5mEE97VC+hIBxQ7B3AF15aHAxrETLJGLGcCoZr1fHt+OuzM8IBmC/WHcIwDsdXy8p1hCVbngSqE9J0kVch0+HAKmpYhlALGMJIXRnYlt9wBmeQ5GCcu6TmxpcU+Zb44C8EfGfKM9PaQVTOE83pCW+vdpB957EhnP537k4HEX2GQgJAgiCFGGEuuhTIpotp7s4RSKw9vhGmIZeHg6BfyKYKiDG16SlpXxMuRnFbN3DY2JJhPiIJgASis4u6uNZZdA5gZhRbVX7QfpVYsUGwVXNeNFtkgIWXCugRs7zTxcxtxqO/ewT+897AMN8PwTRSG4zZW3gmY/XO7mibak4zkInSCTu7SBvpyE+8nu+uwcDniugZOwTCsmlrhRNTwYpRJ1kZNM3fRo7IYUKQrKEZZ3hViGDsQyseFqTNqV8BPDWEFItNgVmd0MhtaFkrrxNrrKyM96bHiokTCZNI1ezplevsjC3scoS4OqTelkWxQUUwRtWoLOptRx9hu9EgTnEF7cnShpGrKP75OuqsivXKDzK784qOKRr+u6xMtUxFJJSeZwUdWJrQhHZnAiWHryQVpCJbaBtBulByz3julnulymC5zgdDYFE81aRJoVd/nEaUA0Kkhu59pbB1kqYfKSpjmDPfQe97iWGqA92abmGx3Zp3GNNJWQSoSHrFw4JGmhBFFQgmUkppKQYBdi1hOSgo+CzlxWd4BzVJKpQclUVhLisGvuLTMtsUlCO96SiAS6hCF4ETX9ScdMkjofcDvdXLkuHv6BpxBCBbzck96sWFWywzpznwlphMZ2lVSY4EoSSCZOr5dgaQeThR8Jbk1VPcaiY3cpehnzhljChItAJST+GHqYCZf+nFkCcr5I8upcR3jUpY8CFO+X87RukZ2NikeumPLNVDV5R5XKdEJ8GFIdpqMgCMqPdE4fMCMeTJJNo8acpy3vAuulbBsovG6RVECprYOZoWNiT6UUMkG9P4i7bS/RJ5jmr/mViqYL+bAkkNriyiCjSvTWJnEJkFmagMZfbRJ6LDef0OrSsyJ/fZDEOwwyUpCJZvW5auGk2cliCzxC8VY2pOQYEl97V4Ra8Bm1qQPg2lTLLmJq8kMsihx2ei+ogWzBOkcDak8Mkls38cDLRRNXEfxJ/qD6jgcZ4WUc2bqdlpziYw3ji2vifeKWrGSliH7LbHftPG0Hz14goL0nAYRzp0DWhTYcQUsO/cLO4Nz2Bi9uhEP0pKWDJV0Urg[TRUNCATED FOR BREVITY IN THIS TURN]";
}
