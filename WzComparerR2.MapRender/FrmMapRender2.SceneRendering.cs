﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using WzComparerR2.Common;
using WzComparerR2.Rendering;
using WzComparerR2.MapRender.UI;
using WzComparerR2.MapRender.Patches2;
using WzComparerR2.Animation;

using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;

namespace WzComparerR2.MapRender
{
    public partial class FrmMapRender2
    {

        private void UpdateAllItems(SceneNode node, TimeSpan elapsed)
        {
            var container = node as ContainerNode;
            if (container != null)  //暂时不考虑缩进z层递归合并  container下没有子节点
            {
                foreach (var item in container.Slots)
                {
                    if (item is BackItem)
                    {
                        var back = (BackItem)item;
                        (back.View.Animator as WzComparerR2.Controls.AnimationItem)?.Update(elapsed);
                        back.View.Time += (int)elapsed.TotalMilliseconds;
                    }
                    else if (item is ObjItem)
                    {
                        var _item = (ObjItem)item;
                        (_item.View.Animator as WzComparerR2.Controls.AnimationItem)?.Update(elapsed);
                        _item.View.Time += (int)elapsed.TotalMilliseconds;
                    }
                    else if (item is TileItem)
                    {
                        var tile = (TileItem)item;
                        (tile.View.Animator as WzComparerR2.Controls.AnimationItem)?.Update(elapsed);
                        tile.View.Time += (int)elapsed.TotalMilliseconds;
                    }
                    else if (item is LifeItem)
                    {
                        var life = (LifeItem)item;
                        var smAni = (life.View.Animator as StateMachineAnimator);
                        if (smAni != null)
                        {
                            if (smAni.GetCurrent() == null) //当前无动作
                            {
                                smAni.SetAnimation(smAni.Data.States[0]); //动作0
                            }
                            smAni.Update(elapsed);
                        }

                        life.View.Time += (int)elapsed.TotalMilliseconds;
                    }
                    else if (item is PortalItem)
                    {
                        var portal = (PortalItem)item;

                        //更新状态
                        var cursorPos = renderEnv.Camera.CameraToWorld(renderEnv.Input.MousePosition);
                        var sensorRect = new Rectangle(portal.X - 250, portal.Y - 150, 500, 300);
                        portal.View.IsFocusing = sensorRect.Contains(cursorPos);

                        //更新动画
                        var ani = portal.View.IsEditorMode ? portal.View.EditorAnimator : portal.View.Animator;
                        if (ani is StateMachineAnimator)
                        {
                            if (portal.View.Controller != null)
                            {
                                portal.View.Controller.Update(elapsed);
                            }
                            else
                            {
                                ((StateMachineAnimator)ani).Update(elapsed);
                            }
                        }
                        else if (ani is FrameAnimator)
                        {
                            var frameAni = (FrameAnimator)ani;
                            frameAni.Update(elapsed);
                        }
                    }
                    else if (item is ReactorItem)
                    {
                        var reactor = (ReactorItem)item;
                        var ani = reactor.View.Animator;
                        if (ani is StateMachineAnimator)
                        {
                            if (reactor.View.Controller != null)
                            {
                                reactor.View.Controller.Update(elapsed);
                            }
                            else
                            {
                                ((StateMachineAnimator)ani).Update(elapsed);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var child in node.Nodes)
                {
                    UpdateAllItems(child, elapsed);
                }
            }
        }

        private void UpdateTooltip()
        {
            var mouse = renderEnv.Input.MousePosition;
            
            var mouseElem = EmptyKeys.UserInterface.Input.InputManager.Current.MouseDevice.MouseOverElement;
            object target = null;
            if (mouseElem == this.ui.ContentControl)
            {
                var mouseTarget = this.allItems.Reverse<ItemRect>().FirstOrDefault(item =>
                {
                    return item.rect.Contains(mouse) && (item.item is LifeItem || item.item is PortalItem || item.item is ReactorItem);
                });
                target = mouseTarget.item;
            }
            else if (mouseElem is ITooltipTarget)
            {
                var pos = EmptyKeys.UserInterface.Input.InputManager.Current.MouseDevice.GetPosition(mouseElem);
                target = ((ITooltipTarget)mouseElem).GetTooltipTarget(pos);
            }
            tooltip.TooltipTarget = target;
        }

        private void DrawScene(GameTime gameTime)
        {
            allItems.Clear();
            var origin = this.renderEnv.Camera.Origin.ToPoint();
            this.batcher.Begin(Matrix.CreateTranslation(new Vector3(-origin.X, -origin.Y, 0)));
            //绘制场景
            foreach (var kv in GetDrawableItems(this.mapData.Scene))
            {
                this.batcher.Draw(kv.Value);

                //绘制标签
                DrawName(kv.Key);

                //缓存绘图区域
                {
                    Rectangle[] rects = this.batcher.Measure(kv.Value);
                    if (kv.Value.RenderObject is Frame)
                    {
                        var frame = (Frame)kv.Value.RenderObject;
                    }
                    if (rects != null && rects.Length > 0)
                    {
                        for (int i = 0; i < rects.Length; i++)
                        {
                            rects[i].X -= origin.X;
                            rects[i].Y -= origin.Y;
                            allItems.Add(new ItemRect() { item = kv.Key, rect = rects[i] });
                        }
                    }
                }
            }

            //在场景之上绘制额外标记
            DrawFootholds(gameTime);

            this.batcher.End();
        }

        private void DrawFootholds(GameTime gameTime)
        {
            var color = MathHelper2.HSVtoColor((float)gameTime.TotalGameTime.TotalSeconds * 100 % 360, 1f, 1f);
            if (patchVisibility.FootHoldVisible)
            {
                var lines = new List<Point>();
                foreach (LayerNode layer in this.mapData.Scene.Layers.Nodes)
                {
                    var fhList = layer.Foothold.Nodes.OfType<ContainerNode<FootholdItem>>()
                        .Select(container => container.Item);
                    foreach (var fh in fhList)
                    {
                        lines.Add(new Point(fh.X1, fh.Y1));
                        lines.Add(new Point(fh.X2, fh.Y2));
                    }
                }

                if (lines.Count > 0)
                {
                    var meshItem = new MeshItem();
                    meshItem.RenderObject = new LineListMesh(lines.ToArray(), color, 2);
                    this.batcher.Draw(meshItem);
                }
            }

            if (patchVisibility.LadderRopeVisible)
            {
                var lines = new List<Point>();
                var ladderList = this.mapData.Scene.Fly.LadderRope.Slots.OfType<LadderRopeItem>();
                foreach (var item in ladderList)
                {
                    lines.Add(new Point(item.X, item.Y1));
                    lines.Add(new Point(item.X, item.Y2));
                }

                if (lines.Count > 0)
                {
                    var meshItem = new MeshItem();
                    meshItem.RenderObject = new LineListMesh(lines.ToArray(), color, 3);
                    this.batcher.Draw(meshItem);
                }
            }

            if (patchVisibility.SkyWhaleVisible)
            {
                var lines = new List<Point>();
                var skyWhaleList = this.mapData.Scene.Fly.SkyWhale.Slots.OfType<SkyWhaleItem>();
                foreach (var item in skyWhaleList)
                {
                    foreach (var dx in new[] { 0, -item.Width / 2, item.Width / 2 })
                    {
                        Point start = new Point(item.Start.X + dx, item.Start.Y);
                        Point end = new Point(item.End.X + dx, item.End.Y);
                        //画箭头
                        lines.Add(start);
                        lines.Add(end);
                        lines.Add(end);
                        lines.Add(new Point(end.X - 5, end.Y + 8));
                        lines.Add(end);
                        lines.Add(new Point(end.X + 5, end.Y + 8));
                    }
                }

                if (lines.Count > 0)
                {
                    var meshItem = new MeshItem();
                    meshItem.RenderObject = new LineListMesh(lines.ToArray(), color, 1);
                    this.batcher.Draw(meshItem);
                }
            }
        }

        private void DrawName(SceneItem item)
        {
            StringResult sr = null;
            MeshItem mesh = null;

            if (item is LifeItem)
            {
                var life = (LifeItem)item;
                switch (life.Type)
                {
                    case LifeItem.LifeType.Mob:
                        {
                            string lv = "Lv." + (life.LifeInfo?.level ?? 0);
                            string name;
                            if (this.StringLinker?.StringMob.TryGetValue(life.ID, out sr) ?? false)
                                name = sr.Name;
                            else
                                name = life.ID.ToString();

                            //绘制怪物名称
                            mesh = new MeshItem()
                            {
                                Position = new Vector2(life.X, life.Cy + 4),
                                RenderObject = new TextMesh()
                                {
                                    Align = Alignment.Center,
                                    ForeColor = Color.White,
                                    BackColor = new Color(Color.Black, 0.7f),
                                    Font = renderEnv.Fonts.MobNameFont,
                                    Padding = new Margins(2, 2, 2, 1),
                                    Text = name
                                }
                            };
                            batcher.Draw(mesh);

                            //绘制怪物等级
                            var nameRect = batcher.Measure(mesh)[0];
                            mesh = new MeshItem()
                            {
                                Position = new Vector2(nameRect.X - 2, nameRect.Y + 3),
                                RenderObject = new TextMesh()
                                {
                                    Align = Alignment.Far,
                                    ForeColor = Color.White,
                                    BackColor = new Color(Color.Black, 0.7f),
                                    Font = renderEnv.Fonts.MobLevelFont,
                                    Padding = new Margins(2, 1, 1, 1),
                                    Text = lv
                                }
                            };
                            batcher.Draw(mesh);
                        }
                        break;

                    case LifeItem.LifeType.Npc:
                        {
                            string name, desc;
                            if (this.StringLinker?.StringNpc.TryGetValue(life.ID, out sr) ?? false)
                            {
                                name = sr.Name;
                                desc = sr.Desc;
                            }
                            else
                            {
                                name = life.ID.ToString();
                                desc = null;
                            }

                            if (name != null)
                            {
                                mesh = new MeshItem()
                                {
                                    Position = new Vector2(life.X, life.Cy + 4),
                                    RenderObject = new TextMesh()
                                    {
                                        Align = Alignment.Center,
                                        ForeColor = Color.Yellow,
                                        BackColor = new Color(Color.Black, 0.7f),
                                        Font = renderEnv.Fonts.NpcNameFont,
                                        Padding = new Margins(2, 2, 2, 1),
                                        Text = name
                                    }
                                };
                                batcher.Draw(mesh);
                            }
                            if (desc != null)
                            {
                                mesh = new MeshItem()
                                {
                                    Position = new Vector2(life.X, life.Cy + 24),
                                    RenderObject = new TextMesh()
                                    {
                                        Align = Alignment.Center,
                                        ForeColor = Color.Yellow,
                                        BackColor = new Color(Color.Black, 0.7f),
                                        Font = renderEnv.Fonts.NpcDescFont,
                                        Padding = new Margins(2, 2, 2, 1),
                                        Text = desc
                                    }
                                };
                                batcher.Draw(mesh);
                            }
                        }
                        break;
                }
            }
        }

        private IEnumerable<KeyValuePair<SceneItem, MeshItem>> GetDrawableItems(SceneNode node)
        {
            var container = node as ContainerNode;
            if (container != null)  //暂时不考虑缩进z层递归合并  container下没有子节点
            {
                foreach (var kv in container.Slots.Select(item => new KeyValuePair<SceneItem, MeshItem>(item, GetMesh(item)))
                    .Where(kv => kv.Value != null)
                    .OrderBy(kv => kv.Value))
                {
                    yield return kv;
                }
            }
            else
            {
                foreach (var mesh in node.Nodes.SelectMany(child => GetDrawableItems(child)))
                {
                    yield return mesh;
                }
            }
        }

        private MeshItem GetMesh(SceneItem item)
        {
            if (item is BackItem)
            {
                var back = (BackItem)item;
                if (back.IsFront ? patchVisibility.FrontVisible : patchVisibility.BackVisible)
                {
                    return GetMeshBack(back);
                }
            }
            else if (item is ObjItem)
            {
                if (patchVisibility.ObjVisible)
                {
                    return GetMeshObj((ObjItem)item);
                }
            }
            else if (item is TileItem)
            {
                if (patchVisibility.TileVisible)
                {
                    return GetMeshTile((TileItem)item);
                }
            }
            else if (item is LifeItem)
            {
                var life = (LifeItem)item;
                if ((life.Type == LifeItem.LifeType.Mob && patchVisibility.MobVisible)
                    || (life.Type == LifeItem.LifeType.Npc && patchVisibility.NpcVisible))
                {
                    return GetMeshLife(life);
                }
            }
            else if (item is PortalItem)
            {
                if (patchVisibility.PortalVisible)
                {
                    return GetMeshPortal((PortalItem)item);
                }
            }
            else if (item is ReactorItem)
            {
                if (patchVisibility.ReactorVisible)
                {
                    return GetMeshReactor((ReactorItem)item);
                }
            }

            return null;
        }

        private MeshItem GetMeshBack(BackItem back)
        {
            //计算计算culling
            if (back.ScreenMode != 0 && back.ScreenMode != renderEnv.Camera.DisplayMode + 1)
            {
                return null;
            }

            //计算坐标
            var renderObject = (back.View.Animator as FrameAnimator)?.CurrentFrame.Rectangle.Size ?? Point.Zero;
            int cx = (back.Cx == 0 ? renderObject.X : back.Cx);
            int cy = (back.Cy == 0 ? renderObject.Y : back.Cy);

            Vector2 tileOff = new Vector2(cx, cy);
            Vector2 position = new Vector2(back.X, back.Y);

            //计算水平卷动
            if (back.TileMode.HasFlag(TileMode.ScrollHorizontial))
            {
                position.X += ((float)back.Rx * 5 * back.View.Time / 1000) % cx;// +this.Camera.Center.X * (100 - Math.Abs(this.rx)) / 100;
            }
            else //镜头移动比率偏移
            {
                position.X += renderEnv.Camera.Center.X * (100 + back.Rx) / 100;
            }

            //计算垂直卷动
            if (back.TileMode.HasFlag(TileMode.ScrollVertical))
            {
                position.Y += ((float)back.Ry * 5 * back.View.Time / 1000) % cy;// +this.Camera.Center.Y * (100 - Math.Abs(this.ry)) / 100;
            }
            else //镜头移动比率偏移
            {
                position.Y += (renderEnv.Camera.Center.Y) * (100 + back.Ry) / 100;
            }

            //y轴镜头调整
            //if (back.TileMode == TileMode.None && renderEnv.Camera.WorldRect.Height > 600)
            //    position.Y += (renderEnv.Camera.Height - 600) / 2;

            //取整
            position.X = (float)Math.Floor(position.X);
            position.Y = (float)Math.Floor(position.Y);

            //计算tile
            Rectangle? tileRect = null;
            if (back.TileMode != TileMode.None)
            {
                var cameraRect = renderEnv.Camera.ClipRect;

                int l, t, r, b;
                if (back.TileMode.HasFlag(TileMode.Horizontal) && cx > 0)
                {
                    l = (int)Math.Floor((cameraRect.Left - position.X) / cx) - 1;
                    r = (int)Math.Ceiling((cameraRect.Right - position.X) / cx) + 1;
                }
                else
                {
                    l = 0;
                    r = 1;
                }

                if (back.TileMode.HasFlag(TileMode.Vertical) && cy > 0)
                {
                    t = (int)Math.Floor((cameraRect.Top - position.Y) / cy) - 1;
                    b = (int)Math.Ceiling((cameraRect.Bottom - position.Y) / cy) + 1;
                }
                else
                {
                    t = 0;
                    b = 1;
                }

                tileRect = new Rectangle(l, t, r - l, b - t);
            }

            //生成mesh
            var renderObj = GetRenderObject(back.View.Animator, back.Flip, back.Alpha);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = position,
                Z0 = 0,
                Z1 = back.Index,
                FlipX = back.Flip,
                TileRegion = tileRect,
                TileOffset = tileOff,
            };
        }

        private MeshItem GetMeshObj(ObjItem obj)
        {
            var renderObj = GetRenderObject(obj.View.Animator, obj.Flip);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = new Vector2(obj.X, obj.Y),
                FlipX = obj.Flip,
                Z0 = obj.Z,
                Z1 = obj.Index,
            };
        }

        private MeshItem GetMeshTile(TileItem tile)
        {
            var renderObj = GetRenderObject(tile.View.Animator);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = new Vector2(tile.X, tile.Y),
                Z0 = ((renderObj as Frame)?.Z ?? 0),
                Z1 = tile.Index,
            };
        }

        private MeshItem GetMeshLife(LifeItem life)
        {
            var renderObj = GetRenderObject(life.View.Animator);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = new Vector2(life.X, life.Cy),
                FlipX = life.Flip,
                Z0 = ((renderObj as Frame)?.Z ?? 0),
                Z1 = life.Index,
            };
        }

        private MeshItem GetMeshPortal(PortalItem portal)
        {
            var renderObj = GetRenderObject(portal.View.IsEditorMode ? portal.View.EditorAnimator : portal.View.Animator);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = new Vector2(portal.X, portal.Y),
                Z0 = ((renderObj as Frame)?.Z ?? 0),
                Z1 = portal.Index,
            };
        }

        private MeshItem GetMeshReactor(ReactorItem reactor)
        {
            var renderObj = GetRenderObject(reactor.View.Animator);
            return renderObj == null ? null : new MeshItem()
            {
                RenderObject = renderObj,
                Position = new Vector2(reactor.X, reactor.Y),
                FlipX = reactor.Flip,
                Z0 = ((renderObj as Frame)?.Z ?? 0),
                Z1 = reactor.Index,
            };
        }

        private object GetRenderObject(object animator, bool flip = false, int alpha = 255)
        {
            if (animator is FrameAnimator)
            {
                var frame = ((FrameAnimator)animator).CurrentFrame;
                if (frame != null)
                {
                    if (alpha < 255) //理论上应该返回一个新的实例
                    {
                        frame.A0 = frame.A0 * alpha / 255;
                    }
                    return frame;
                }
            }
            else if (animator is SpineAnimator)
            {
                var skeleton = ((SpineAnimator)animator).Skeleton;
                if (skeleton != null)
                {
                    if (alpha < 255)
                    {
                        skeleton.A = alpha / 255.0f;
                    }
                    return skeleton;
                }
            }
            else if (animator is StateMachineAnimator)
            {
                var smAni = (StateMachineAnimator)animator;
                return smAni.Data.GetMesh();
            }

            //各种意外
            return null;
        }
    }
}
