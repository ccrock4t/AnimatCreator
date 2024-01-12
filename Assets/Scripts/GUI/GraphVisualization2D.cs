using System.Collections.Generic;

// from https://llimllib.github.io/pymag-trees/
public class GraphVisualization2D
{
    public class PositionData2D
    {
        public float x;
        public float y;
        public float mod;
        public PositionData2D? thread;
        public float offset = 0;
        public PositionData2D ancestor;
        public float change = 0;
        public float shift = 0;
        public int index = 0;
        public PositionData2D? _lmost_sibling;
        public List<PositionData2D> children;
        public PositionData2D? parent;

        public PositionData2D(PositionData2D? parent)
        {
            this.x = 0;
            this.y = 0;
            this.mod = 0;
            this.shift = 0;
            this.change = 0;
            this.thread = null;
            this.ancestor = this;
            this.children = new();
            this.parent = parent;
        }


        public PositionData2D? left_brother()
        {
            PositionData2D? n = null;
            if (this.parent != null)
            {
                foreach (PositionData2D node in this.parent.children)
                {
                    if (node == this)
                    {
                        return n;
                    }
                    else
                    {
                        n = node;
                    }
                }
            }
            return n;
        }

        public PositionData2D? left()
        {
            if (this.thread != null)
            {
                return this.thread;
            }
            else
            {
                if (this.children.Count > 0)
                {
                    return this.children[0];
                }
            }
            return null;
        }

        public PositionData2D? right()
        {

            if (this.thread != null)
            {
                return this.thread;
            }
            else
            {
                if (this.children.Count > 0)
                {
                    return this.children[this.children.Count - 1];
                }
            }

            return null;
        }

        public PositionData2D? get_lmost_sibling()
        {
            if (this.parent == null || this.parent.children[0] == this) return null;
            return this.parent.children[0];
        }
    }


    public static PositionData2D setup(PositionData2D tree)
    {

        PositionData2D dt = firstwalk(tree);
        float min = second_walk(dt);
        if (min < 0)
        {
            third_walk(dt, -min);
        }
        return dt;
    }

    public static PositionData2D firstwalk(PositionData2D v, float distance = 1.0f)
    {
        if (v.children.Count == 0)
        {
            if (v.get_lmost_sibling() != null)
            {
                v.x = v.left_brother().x + distance;
            }
            else
            {
                v.x = 0.0f;
            }
        }
        else
        {
            PositionData2D default_ancestor = v.children[0];
            foreach (PositionData2D k in v.children)
            {
                firstwalk(k);
                default_ancestor = apportion(k, default_ancestor, distance);
            }
            execute_shifts(v);
            float midpoint = (v.children[0].x + v.children[v.children.Count - 1].x) / 2;
            var ell = v.children[0];
            var arr = v.children[v.children.Count - 1];
            PositionData2D? w = v.left_brother();
            if (w != null)
            {
                v.x = w.x + distance;
                v.mod = v.x - midpoint;
            }
            else
            {
                v.x = midpoint;
            }
        }
        return v;
    }

    public static PositionData2D apportion(PositionData2D v, PositionData2D default_ancestor, float distance)
    {
        PositionData2D? w = v.left_brother();
        if (w != null)
        {
            //in buchheim notation:
            //i == inner; o == outer; r == right; l == left;
            PositionData2D vir = v;
            PositionData2D vor = v;
            PositionData2D vil = w;
            PositionData2D vol = v.get_lmost_sibling();
            float sir = v.mod;
            float sor = v.mod;
            float sil = vil.mod;
            float sol = vol.mod;
            while (vil.right() != null && vir.left() != null)
            {
                vil = vil.right();
                vir = vir.left();
                vol = vol.left();
                vor = vor.right();


                var shift = vil.x + sil - (vir.x + sir) + distance;
                if (shift > 0)
                {
                    PositionData2D a = ancestor(vil, v, default_ancestor);
                    move_subtree(a, v, shift);
                    sir = sir + shift;
                    sor = sor + shift;
                }
                sil += vil.mod;
                sir += vir.mod;
                sol += vol.mod;
                sor += vor.mod;
            }
            if (vil.right() != null && vor.right() == null)
            {
                vor.thread = vil.right();
                vor.mod += sil - sor;
            }
            else
            {
                if (vir.left() != null && vol.left() == null)
                {
                    vol.thread = vir.left();
                    vol.mod += sir - sol;
                }
                default_ancestor = v;
            }
        }
        return default_ancestor;
    }

    public static void move_subtree(PositionData2D wl, PositionData2D wr, float shift)
    {
        int subtrees = wr.index - wl.index;
        wr.change -= shift / subtrees;
        wr.shift += shift;
        wl.change += shift / subtrees;
        wr.x += shift;
        wr.mod += shift;
    }

    public static void execute_shifts(PositionData2D v)
    {
        float shift = 0;
        float change = 0;
        foreach (var w in v.children)
        {
            w.x += shift;
            w.mod += shift;
            change += w.change;
            shift += w.shift + change;
        }
    }

    public static PositionData2D ancestor(PositionData2D vil, PositionData2D v, PositionData2D default_ancestor)
    {
        if (v.parent.children.IndexOf(vil.ancestor) != -1)
        {
            return vil.ancestor;
        }
        else
        {
            return default_ancestor;
        }
    }

    public static float second_walk(PositionData2D v, float m = 0, int depth = 0, float? min = null)
    {
        v.x += m;
        v.y = depth;
        if (min == null || v.x < min)
        {
            min = v.x;
        }
        foreach (var w in v.children)
        {
            min = second_walk(w, m + v.mod, depth + 1, min);
        }
        return (float)min;
    }

    public static void third_walk(PositionData2D tree, float n)
    {
        tree.x += n;
        foreach (var c in tree.children)
        {
            third_walk(c, n);
        }
    }

}
