namespace ClipFlow.Core.Imaging;

public static class ImageAnalysis
{
    /// <summary>
    /// 全画素が黒か。DRM 保護コンテンツ等が取得できず黒く写った可能性をユーザーに知らせる判定に使う。
    /// </summary>
    public static bool IsEntirelyBlack(RawImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var px = image.Pixels;
        for (var i = 0; i < px.Length; i += RawImage.BytesPerPixel)
        {
            if (px[i] != 0 || px[i + 1] != 0 || px[i + 2] != 0)
            {
                return false;
            }
        }

        return px.Length > 0;
    }
}
