

using OpenCvSharp;
using System;
using System.Collections.Generic;
public static class ZikZakClass
{
    /// <summary>
    /// Adjust image orientation based on SKU reference points.
    /// </summary>
    /// <param name="imagePath">Input image path</param>
    /// <param name="skuPoints">List of SKU points in correct order (TopLeft, TopRight, BottomLeft, BottomRight)</param>
    /// <returns>Correctly oriented Mat image</returns>
    public static Mat AdjustImageBySkuPoints(string imagePath, List<Point2f> skuPoints)
    {
        if (skuPoints == null || skuPoints.Count < 2)
            throw new ArgumentException("At least TopLeft and TopRight points are required.");

        // Load image
        Mat image = Cv2.ImRead(imagePath);

        // Calculate angle using top left & top right points
        Point2f topLeft = skuPoints[0];
        Point2f topRight = skuPoints[1];

        // Difference
        float dx = topRight.X - topLeft.X;
        float dy = topRight.Y - topLeft.Y;

        // Angle in degrees
        double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

        // Normalize angle (0° to 360°)
        if (angle < 0)
            angle += 360;

        // Check which quadrant and adjust
        double rotationNeeded = 0;
        if (Math.Abs(angle) < 45) rotationNeeded = 0;                // Correct
        else if (Math.Abs(angle - 90) < 45) rotationNeeded = -90;    // 90° rotated
        else if (Math.Abs(angle - 180) < 45) rotationNeeded = -180;  // 180° rotated
        else if (Math.Abs(angle - 270) < 45) rotationNeeded = -270;  // 270° rotated

        // Rotate image
        Mat rotated = new Mat();
        var center = new Point2f(image.Width / 2f, image.Height / 2f);
        Mat rotationMatrix = Cv2.GetRotationMatrix2D(center, rotationNeeded, 1.0);
        Cv2.WarpAffine(image, rotated, rotationMatrix, image.Size());

        return rotated;
    }
}

