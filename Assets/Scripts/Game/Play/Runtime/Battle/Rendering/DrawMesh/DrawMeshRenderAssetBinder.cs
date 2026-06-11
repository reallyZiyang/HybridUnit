namespace Game.Play.Battle.Rendering
{
    internal sealed class DrawMeshRenderAssetBinder
    {
        private readonly DrawMeshUnitRenderer unitRenderer;
        private readonly DrawMeshEffectRenderer effectRenderer;

        public DrawMeshRenderAssetBinder(DrawMeshUnitRenderer unitRenderer, DrawMeshEffectRenderer effectRenderer)
        {
            this.unitRenderer = unitRenderer;
            this.effectRenderer = effectRenderer;
        }

        public void Bind(
            BattleRenderEntry entry,
            BattleRenderAssetBase asset,
            UnitDrawRenderState unitState,
            EffectDrawRenderState effectState)
        {
            if (entry == null || asset == null)
            {
                return;
            }

            switch (asset)
            {
                case BakedAnimationVitAsset animationAsset:
                    if (unitState != null)
                    {
                        unitRenderer.BindAnimationVit(entry, unitState, animationAsset);
                    }

                    break;
                case BakedSpineVitAsset spineAsset:
                    if (unitState != null)
                    {
                        unitRenderer.BindSpineVit(entry, unitState, spineAsset);
                    }

                    break;
                case BakedSequenceAsset sequenceAsset:
                    if (effectState != null)
                    {
                        effectRenderer.BindSequence(entry, effectState, sequenceAsset);
                    }

                    break;
                case AtlasRenderAsset atlasAsset:
                    effectRenderer.BindAtlas(entry, atlasAsset);
                    break;
            }
        }
    }
}
