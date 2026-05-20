using System.Collections;
using Assets._Keystone.Runtime.Scripts.Events;
using UnityEngine;

namespace Assets._Keystone.Runtime.Scripts.Networking.SaveNetwork
{
    public class PlayerSaveSync : MonoBehaviour
    {
        [SerializeField] private float autoSyncInterval = 30f;
        [SerializeField] private bool useAutoSync = true;

        private PlayerSaveAgent _agent;
        private Coroutine _autoSyncCoroutine;

        private void Awake()
        {
            _agent = GetComponent<PlayerSaveAgent>();
        }

        private void Start()
        {
            if (useAutoSync)
                _autoSyncCoroutine = StartCoroutine(AutoSyncRoutine());
        }

        void OnEnable()
        {
            KeystoneEvents.OnPlayerSyncRequested += SyncNow;
        }

        private IEnumerator AutoSyncRoutine()
        {
            var wait = new WaitForSeconds(autoSyncInterval);
            while (true)
            {
                yield return wait;

                if (_agent != null && _agent.IsOwner)
                    _agent.PushLocalModulesToServer();
            }
        }

        public void SyncNow()
        {
            _agent?.PushLocalModulesToServer();
            Debug.Log("Sincronizando");
        }

        private void OnDestroy()
        {
            if (_autoSyncCoroutine != null)
                StopCoroutine(_autoSyncCoroutine);

            KeystoneEvents.OnPlayerSyncRequested -= SyncNow;
        }
    }
}