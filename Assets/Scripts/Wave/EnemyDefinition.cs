using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 一种敌人的身份：指向它使用的外观 prefab。
    /// 波次数据以它为单位来挑选敌人；生成器只读这里的 prefab 去实例化。
    /// </summary>
    [CreateAssetMenu(menuName = "Decoherence/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("这类敌人的外观 prefab")]
        [SerializeField]
        GameObject prefab;

        public GameObject Prefab => prefab;
    }
}
