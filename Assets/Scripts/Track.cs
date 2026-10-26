using System.Collections.Generic;
using UnityEngine;

namespace WobbleWaiter
{
    // The winding path through the restaurant, sampled every DS meters.
    // h is the heading in radians (0 = +z, positive turns right); k is the curvature (positive = left turn).
    public class Track
    {
        public const float DS = 0.25f;

        public struct Pt { public float x, z, h, k; }

        readonly List<Pt> pts = new List<Pt>();
        float x, z, h, s, pieceLeft, pieceK;
        bool lastTurn;

        public float Length => s;

        public void Reset()
        {
            pts.Clear();
            x = z = h = s = 0; pieceLeft = 0; pieceK = 0; lastTurn = false;
            pts.Add(new Pt());
            Extend(90);
        }

        void NextPiece()
        {
            // a straight start to learn on, then turns with short straights between them
            if (s < 34) { pieceLeft = 34 - s; pieceK = 0; return; }
            if (lastTurn) { lastTurn = false; pieceLeft = Random.Range(7f, 14f); pieceK = 0; return; }
            lastTurn = true;
            int dir = Random.value < 0.5f ? -1 : 1;
            // steer the heading back toward the start direction so the path never loops onto itself
            if (h > 0.35f) dir = 1; else if (h < -0.35f) dir = -1;
            float radius = Random.Range(8f, 12f), angle = Random.Range(Mathf.PI / 5f, Mathf.PI / 3f);
            pieceLeft = radius * angle;
            pieceK = dir / radius;
        }

        public void Extend(float toS)
        {
            while (s < toS)
            {
                if (pieceLeft <= 0) NextPiece();
                h -= pieceK * DS;
                x += Mathf.Sin(h) * DS;
                z += Mathf.Cos(h) * DS;
                s += DS;
                pieceLeft -= DS;
                pts.Add(new Pt { x = x, z = z, h = h, k = pieceK });
            }
        }

        public Pt At(float sq)
        {
            float f = Mathf.Max(0, sq) / DS;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, pts.Count - 2);
            float t = Mathf.Clamp01(f - i);
            var a = pts[i]; var b = pts[i + 1];
            return new Pt { x = Mathf.Lerp(a.x, b.x, t), z = Mathf.Lerp(a.z, b.z, t), h = Mathf.Lerp(a.h, b.h, t), k = b.k };
        }

        public Vector3 Place(float sq, float u, float y = 0)
        {
            var p = At(sq);
            return new Vector3(p.x + Mathf.Cos(p.h) * u, y, p.z - Mathf.Sin(p.h) * u);
        }

        public static Vector3 Forward(float h) => new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
        public static Vector3 Right(float h) => new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h));
    }
}
