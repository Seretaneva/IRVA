# Investigatie L2: selectarea si mutarea punctelor

## Remediere aplicata

Singura modificare functionala a acestei investigatii este in ProjectSettings/DynamicsManager.asset:

```diff
-  m_CurrentBackendId: 3737844653
+  m_CurrentBackendId: 4072204805
```

Valorile au fost obtinute din inventarul backend-urilor instalarii Unity 6000.3.25f1, nu deduse din alte versiuni. Prima valoare este None (0xdecafbad), a doua PhysX (0xf2b8ea05).
Celelalte diferente din acest fisier fata de HEAD existau deja. Nu s-au schimbat scripturile, comentariile, TODO-urile, scenele, prefab-urile, GUID-urile, input-ul sau pachetele.

## Dovezi si traseul atingerii

- Backend-ul activ in Editorul proiectului a raportat name=None, isFallback=True, description="Null backend implementation, no operations will be executed when using this backend."
- Un cub de control creat cu GameObject.CreatePrimitive, la o pozitie izolata, nu era lovit de Physics.Raycast nici dupa Physics.SyncTransforms(). Rezultat: False, collider=none.
- MeasureDistances citeste o singura apasare prin Touchscreen.current.primaryTouch.press.wasPressedThisFrame si construieste raza cu FirstPersonCamera.ScreenPointToRay.
- Referinta FirstPersonCamera si XROrigin.m_Camera indica aceeasi Camera, fileID 1364840131. Exista o singura camera, un XR Origin, un AR Session si o singura instanta MeasureDistances in scena L2.
- Selectarea foloseste Physics.Raycast si verifica atat radacina punctului, cat si copiii. Cu backend None, ramura nu identifica niciun punct. Codul continua cu ARRaycastManager.Raycast(PlaneWithinPolygon), care utilizeaza subsistemul AR, si creeaza un punct nou daca selectedPoint este null. Aceasta explica simptomele fara a presupune un al doilea script de plasare.
- Dupa selectarea reusita exista deja return, deci aceeasi atingere nu ajunge la Instantiate. Atingerea urmatoare pe plan muta acelasi selectedPoint si il deselecteaza. LateUpdate recalculeaza toate segmentele, textele partiale si totalul; astfel sunt acoperite si segmentele adiacente unui punct intermediar.
- Prefab-ul punctului are BoxCollider activ, non-trigger, pe copilul Cube, layer 0. Nu exista o masca Physics care sa excluda acest layer. Camera reda toate layer-ele.
- Scena L2 refera deja AR Line.prefab, separat de AR Placed Cube.prefab. Linia nu are collider. Prefab-ul planului are MeshCollider, dar acesta nu explica ratarea cubului izolat cu backend None. Nu s-a aplicat o schimbare speculativa de selectie pentru plane.
- CloudAnchorObjectPlacement este atasat doar in L3, nu in L2 si nici pe prefab-urile punctului/liniei. Nu s-au gasit DontDestroyOnLoad sau incarcari aditive in codul Assets. Navigarea foloseste SceneManager.LoadScene implicit Single, iar L2 este la indexul 2 in build settings.
- Scripturile proprii de atingere folosesc Input System; activeInputHandler este deja 1. Nu s-a modificat input-ul.

## Istoric si celelalte laboratoare

Git contine multe modificari locale si fisiere noi preexistente, inclusiv MeasureDistances si prefab-urile. HEAD pentru scena L2 este un schelet anterior configurarii actuale. Nu exista o versiune comisa a implementarii locale din care sa se determine exact momentul regresiei. Setarea None este confirmata drept cauza tehnica; atribuirea ei modificarilor Cloud Anchors nu este demonstrata de istoric.

Restabilirea PhysX readuce interogarile collider-elor pentru toate scenele. Nu necesita schimbarea logicii Cloud Anchors sau image tracking. ARRuntimeImageLibrary foloseste ARTrackedImageManager si evenimente AR, nu Physics.Raycast; nu exista dovezi ca problema raportata la image tracking este rezolvata prin aceasta remediere. Nu s-a modificat L4.

## Validare executata

Un proiect temporar izolat in Temp/L2PhysicsValidation a fost pornit cu aceeasi versiune Unity, in batch mode, cu setarea Physics corectata. Editorul utilizatorului nu a fost inchis si scenele sale nu au fost schimbate de teste.

- Backend incarcat dupa pornire: PhysX.
- Aceeasi raza pe cubul de control: True, collider=Cube (inainte: False).
- Trei instante ale prefab-ului real de punct: collider-ul copil Cube identificat corect pentru primul, intermediar si ultimul punct.
- Dupa schimbarea pozitiilor fiecarui punct si Physics.SyncTransforms: identificarea corecta a tuturor celor trei instante.
- Prefab-ul real al liniei: zero collidere; raza prin linie nu este interceptata.
- Scriptul temporar de diagnostic a compilat si a rulat in Editor; prima incercare a sondei a folosit un API intern inaccesibil, apoi a fost corectata si recompilata. Toate sondele au fost eliminate din Assets. Remedierea finala nu modifica niciun fisier C#.
- git diff --check pentru fisierul functional modificat: fara erori.

Loguri locale ale proceselor izolate: Temp/L2PhysicsValidation/validation.log si Temp/L2PhysicsValidation/prefab-validation.log. Scriptul reproductibil al sondei ramane exclusiv in proiectul temporar, nu in Assets al aplicatiei.

Aceste teste verifica backend-ul si lovirea/identitatea collider-elor, nu simuleaza o sesiune ARCore sau atingerea/mutarea printr-un plan AR. Actualizarea masuratorilor si tranzitiile de scena au fost urmarite static, nu validate pe dispozitiv.

## Verificare ramasa pe telefon

Editorul deja pornit a pastrat backend None dupa reincarcarea scripturilor, desi fisierul este corect. Reporneste Editorul inainte de Play Mode/build si genereaza un APK nou; APK-ul vechi nu contine remedierea.

Pe telefon: pornire directa L2 si acces din meniu dupa L1/L3/L4; plasare a minimum trei puncte; selectare apoi mutare pe plan a primului, ultimului si intermediarului; numar de puncte constant la selectie/mutare; actualizarea ambelor segmente vecine si a totalului; atingere pe o linie (nu trebuie confundata cu selectarea unui punct); atingere fara plan valid (selectia ramane pentru o atingere valida). Verifica separat Cloud Anchors si image tracking.

Nu s-a executat build Android sau test pe telefon in aceasta investigatie.
