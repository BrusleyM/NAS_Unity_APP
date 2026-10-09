using GLTFast;
using NAS.Core.Events;
using NAS.Core.Interfaces;
using UnityEngine;

namespace NAS.Core
{
    /// <summary>
    /// Downloads the selected car's 3D model from Tigris and hands it to the
    /// AR placement pipeline as what gets spawned on tap, replacing the
    /// scene's default placeholder prefab. Lives on the same GameObject as
    /// ObjectPlacerController and its IARPlacementService implementation -
    /// self-discovers both via GetComponent rather than Inspector wiring.
    ///
    /// AR Scene is loaded once and never reloaded (see
    /// ParentPageController.OnCarSelected) - the download-and-build logic
    /// used to live directly in Start() since that only ever ran once per
    /// visit under the old reload-every-time design. It's now
    /// LoadSelectedCarAsync(), called once from Start() and again on every
    /// EnterArRequestedEvent, since re-entering AR might mean a different
    /// car was selected the second time around.
    ///
    /// Any failure (no key on the selected car, download error, glTF parse
    /// error) falls back to whatever placeholder prefab is already assigned
    /// on the placement service - this never blocks placement outright.
    /// </summary>
    public class SelectedCarModelLoader : MonoBehaviour
    {
        private ObjectPlacerController _objectPlacer;
        private IARPlacementService _placementService;
        // Tracks what LoadSelectedCarAsync() built last time, so a re-entry
        // (possibly with a different car selected) can clean up the previous
        // model instead of leaking it - the object this points to is the
        // same one ObjectPlacerController repositions/reactivates in place on
        // tap (RaycastPrefabIsLiveInstance), so it's the only copy that exists.
        private GameObject _currentModelRoot;

        private void OnEnable()
        {
            EventBus.Subscribe<EnterArRequestedEvent>(OnEnterAr);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnterArRequestedEvent>(OnEnterAr);
        }

        private void Start()
        {
            _objectPlacer = GetComponent<ObjectPlacerController>();
            _placementService = GetComponent<IARPlacementService>();

            if (_objectPlacer == null || _placementService == null)
            {
                Debug.LogError("SelectedCarModelLoader: ObjectPlacerController or IARPlacementService missing on this GameObject.");
                return;
            }

            LoadSelectedCarAsync();
        }

        private void OnEnterAr(EnterArRequestedEvent evt)
        {
            if (_objectPlacer == null || _placementService == null) return; // Start() failed its dependency check - nothing to do
            LoadSelectedCarAsync();
        }

        private async void LoadSelectedCarAsync()
        {
            if (_currentModelRoot != null)
            {
                Destroy(_currentModelRoot);
                _currentModelRoot = null;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.CurrentArModelLoadSucceeded = false;

            EventBus.Publish(new LoadingStartedEvent("Loading vehicle..."));
            try
            {
                var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
                string modelKey = selectedCar != null ? selectedCar.tigrisModelKey : null;

                if (string.IsNullOrEmpty(modelKey))
                {
                    Debug.LogWarning("SelectedCarModelLoader: no tigrisModelKey for the selected car - using the default placeholder.");
                    EventBus.Publish(new ArModelLoadFailedEvent("no_model_key"));
                    _objectPlacer.EnablePlacement();
                    return;
                }

                var downloadResult = await GameManager.Instance.DownloadModel(modelKey);
                if (this == null) return; // AR Scene unloaded mid-download

                if (!downloadResult.IsSuccess)
                {
                    Debug.LogWarning($"SelectedCarModelLoader: download failed for '{modelKey}': {downloadResult.ErrorMessage} - using the default placeholder.");
                    EventBus.Publish(new ArModelLoadFailedEvent("download"));
                    _objectPlacer.EnablePlacement();
                    return;
                }

                var gltf = new GltfImport();
                bool loadOk = await gltf.Load(downloadResult.Value);
                if (this == null) return;

                if (!loadOk)
                {
                    Debug.LogWarning($"SelectedCarModelLoader: glTF parse failed for '{modelKey}' - using the default placeholder.");
                    EventBus.Publish(new ArModelLoadFailedEvent("parse"));
                    _objectPlacer.EnablePlacement();
                    return;
                }

                // Parked far below the scene AND kept inactive during the async
                // build below - InstantiateMainSceneAsync needs the GameObject
                // active while it runs, so it can't be deactivated up front. Once
                // built, it's deactivated (see below) and reused directly as the
                // placed instance on tap (ObjectPlacerController repositions +
                // reactivates it rather than cloning it - see
                // RaycastPrefabIsLiveInstance), so the -1000 position only matters
                // for the brief window while it's still active and being built.
                // modelKey contains '/' (e.g. "carmodels/bmwm5demo.glb") - GameObject.Find()
                // and Transform.Find() both treat '/' as a hierarchy path separator, not a
                // literal character, so a raw modelKey in the name breaks any future
                // name-based lookup of this object. Sanitize it here instead.
                var safeModelKey = modelKey.Replace('/', '_');
                var modelRoot = new GameObject("SelectedCarModel_" + safeModelKey);
                modelRoot.transform.position = new Vector3(0f, -1000f, 0f);
                _currentModelRoot = modelRoot;

                bool instantiateOk = await gltf.InstantiateMainSceneAsync(modelRoot.transform);
                if (this == null) return;

                if (!instantiateOk)
                {
                    Debug.LogWarning($"SelectedCarModelLoader: instantiate failed for '{modelKey}' - using the default placeholder.");
                    Destroy(modelRoot);
                    _currentModelRoot = null;
                    EventBus.Publish(new ArModelLoadFailedEvent("instantiate"));
                    _objectPlacer.EnablePlacement();
                    return;
                }

                // Discover the car's Body/Door_*/Wheels/etc. renderers once
                // here - CarPaintController (and future consumers like a
                // door-open animation) read this instead of re-deriving
                // categories from material names themselves.
                modelRoot.AddComponent<CarComponents>();
                ContactShadowFactory.Attach(modelRoot);

                modelRoot.SetActive(false);
                _placementService.RaycastPrefab = modelRoot;
                _placementService.RaycastPrefabIsLiveInstance = true;
                Debug.Log($"SelectedCarModelLoader: '{modelKey}' ready to place.");
                if (GameManager.Instance != null)
                    GameManager.Instance.CurrentArModelLoadSucceeded = true;
                _objectPlacer.EnablePlacement();
            }
            finally
            {
                EventBus.Publish(new LoadingFinishedEvent());
            }
        }
    }
}
