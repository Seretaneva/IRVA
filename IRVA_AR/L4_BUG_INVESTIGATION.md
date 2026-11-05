# Regresii de pornire si tranzitie L4

Investigatia curenta, stack trace-urile si validarea pe telefon sunt in [Diagnostics/L4Regression/INVESTIGATION.md](Diagnostics/L4Regression/INVESTIGATION.md). Sectiunile de mai jos descriu etapele anterioare; rezultatele simularilor nu dovedesc stabilitatea nativa pe dispozitiv.

---

# Actualizare: augmentari persistente si cuburi statice de 5 cm

Aceasta sectiune descrie comportamentul curent si inlocuieste regulile de vizibilitate si durata de viata din investigatia initiala pastrata mai jos.

- Un singur cub pentru fiecare imagine de referinta, indiferent de ordinea scanarii si de schimbarea TrackableId la redetectare. Cele trei imagini statice folosesc GUID-urile proprii; imaginea online are o identitate de continut stabila pe durata laboratorului, inclusiv daca biblioteca runtime este recreata.
- Numai Tracking creeaza sau actualizeaza pose-ul cubului. Daca prima observatie este Limited/None, se asteapta primul pose valid. Dupa aceasta, Limited/None pastreaza cubul vizibil la ultima pozitie si orientare valide.
- Cuburile sunt obiecte world-space fara parinte trackable. removed elimina doar asocierea cu TrackableId; cubul nu este distrus si nu urmeaza eventualele transformari invalide ale trackable-ului. Redetectarea aceleiasi imagini realiniaza acelasi cub.
- Comutarea bibliotecilor pastreaza cuburile deja create. Numai iesirea/dezactivarea laboratorului goleste colectiile, ascunde imediat toate cuburile si apeleaza Destroy. Cleanup-ul asincron implementat anterior ramane activ.
- Pentru fiecare imagine statica: root prefab 1 x multiplicator 0.5 x copil Cube 0.1 x mesh unitate = latura world-space 0.05 m. Centrul copilului este la 0.05 x 0.5 = 0.025 m deasupra imaginii; baza ramane pe planul imaginii. Radacina nu are parinte, deci dimensiunile nu depind de dimensiunea imaginii sau de scara XR Origin/trackable-ului.
- Imaginea online pastreaza multiplicatorul 1 si cubul de 0.1 m. Prefab-ul comun, scena si alte laboratoare nu au fost modificate in aceasta actualizare. Modificarea functionala este numai in Assets/Scripts/ARRuntimeImageLibrary.cs. Toate cele 26 de comentarii existente la inceputul actualizarii au fost pastrate textual si in aceeasi ordine.

Compilarea C# cu referintele proiectului a trecut. Testele simulate au trecut (ALL TESTS PASSED): trei ordini de scanare, toate cele trei cuburi de 5 cm cu parinti AR scalati neuniform (2,3,4), baza centrata pe imagine, pastrarea pose-ului la Limited/None, removed fara distrugerea cubului, redetectare cu TrackableId nou si 20 actualizari fara duplicate, trei cicluri de cleanup, persistenta la comutarea bibliotecilor si cub online ramas de 10 cm. Proiect izolat: Temp/L4Validation, log validation-persistent.log. Detectia ARCore, aspectul pe telefon si distrugerea la finalul unui cadru real necesita verificare pe dispozitiv. Pentru Back se foloseste acelasi cleanup OnDisable, iar navigarea reala descarca scena prin LoadScene Single.

---
# L4 Image Tracking: investigatie, remediere si verificare

## Modificari aplicate

- Assets/Scripts/ARRuntimeImageLibrary.cs: un singur proprietar al augmentarilor pentru ambele biblioteci; dictionar dupa TrackableId; abonare/dezabonare la trackablesChanged din AR Foundation 6; actualizare added/updated si eliminare removed.Key; vizibilitate numai pentru Tracking; pose local zero sub imagine; cleanup si anularea operatiilor la OnDisable.
- Assets/Scenes/L4_AugmentedImages.unity: eliminarea Camera, ARCameraManager si UniversalAdditionalCameraData de pe Camera Offset. Main Camera ramane camera XROrigin, cu ARCameraBackground, TrackedPoseDriver si ARCameraManager World. Tracked Image Prefab este null; continutul este creat exclusiv de script. Referinte explicite pentru biblioteca statica, textul butonului, URL si latimea runtime. Butonul comuta modurile si este ancorat jos, in interiorul ecranului.
- Niciun prefab nu necesita modificare: ImageAugumentation are deja radacina la origine, rotatie identitate si scala unitara; copilul Cube este la (0, 0.05, 0), cu scala (0.1, 0.1, 0.1). Baza cubului coincide cu planul imaginii. Scriptul reseteaza explicit pose-ul local al radacinii augmentarii.

Biblioteca statica si prefab-ul sunt identice cu copiile initiale (SHA256 verificat). GUID-urile si referintele pastrate nu au fost regenerate. Nu s-au modificat celelalte scene, navigarea comuna, pachetele sau setarile globale in aceasta interventie. Toate comentariile originale din script, inclusiv TODO-urile, au fost pastrate textual si in aceeasi ordine. TODO-urile vechi despre distrugerea/adaugarea managerului sunt pastrate, cu comentarii suplimentare care explica reutilizarea managerului pentru API-ul instalat.

## Cauze confirmate si limitele concluziilor

1. Scena avea doua Camera si doua ARCameraManager active: pe Camera Offset si pe copilul Main Camera. Numai copilul avea TrackedPoseDriver si ARCameraBackground, iar XROrigin il referea pe acesta. Aceasta configuratie poate produce randare concurenta cu pozitii/proiectii diferite; a fost eliminata camera suplimentara. Nu exista o captura sau log AR care sa dovedeasca exact cauza cubului aparut in colt pentru o anumita imagine.
2. Fluxul static folosea ImageAugumentation ca trackedImagePrefab, adica obiectul trackable insusi era vizual. Scriptul nu se abona la evenimente pentru biblioteca statica. Nu exista control al vizibilitatii pe trackingState; un trackable Limited sau None putea ramane vizibil la ultima pozitie.
3. Fluxul runtime distrugea managerul configurat si adauga altul, cu alt mecanism de augmentare; nu exista revenire la biblioteca statica. Apasari repetate puteau porni coroutine concurente. Nu exista evidenta dupa TrackableId, cleanup sau dezabonare explicita, iar updated si removed erau goale.
4. Back este legat de SceneManagement.MainMenu, care foloseste SceneManager.LoadScene(0), implicit Single. Scena este descarcata; nu este doar ascuns un panou. In scripturile Assets nu exista DontDestroyOnLoad si nu s-a gasit alt script care sa creeze augmentari de imagini. Prin urmare supravietuirea cuburilor intre scene NU a fost confirmata. Problema confirmata este vizibilitatea veche in cursul aceleiasi sesiuni si lipsa unui ciclu explicit de cleanup. Noul OnDisable acopera atat descarcarea scenei, cat si dezactivarea/reactivarea componentei sau obiectului.
5. Camera din spate era deja World (valoare 1), iar ARSession era deja PositionAndRotation (valoare 2). Nu era necesara modificarea lor si nu s-a gasit ARFaceManager in L4_AugmentedImages.

AR Foundation si ARCore instalate sunt ambele 6.3.5 (manifestele din Library/PackageCache). Implementarea foloseste UnityEvent trackablesChanged; removed contine perechi TrackableId/ARTrackedImage. Sursa API instalata a fost verificata direct. Documentatie: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/image-tracking.html

## Biblioteci si imagini

| Nume in biblioteca | Sursa | GUID imagine | Dimensiuni pixeli | Dimensiune fizica statica | Scor arcoreimg |
| --- | --- | --- | --- | --- | --- |
| caine1 | Assets/ImageTracking/caine.jpg | c56720b0-9ad5-4bc0-be09-54c41096c066 | 300 x 300 | Nespecificata | 100 |
| planet | Assets/ImageTracking/planet.jpg | d2415240-28a7-4853-bd65-66936a4d135a | 300 x 300 | Nespecificata | 100 |
| caien2 | Assets/ImageTracking/dog2.jpg | 4e175a2f-5516-41c1-8171-a91500b04700 | 300 x 300 | Nespecificata | 85 |

Cele trei referinte la texturi au fost rezolvate din SerializedTextureGuid si corespund fisierelor .meta. Numele original caien2 nu a fost schimbat. m_Texture este null deoarece nu se pastreaza textura in aplicatie; referinta Editor prin GUID si baza compilata sunt valide. m_SpecifySize=0 este acceptat de fluxul ARCore cu estimarea dimensiunii; nu s-au inventat masuratori fizice pentru imaginile utilizatorului.

Runtime: URL-ul real este https://raw.githubusercontent.com/opencv/opencv/4.x/samples/data/graf1.png . Descarcare verificata: HTTP 200, Content-Type image/png, 951440 octeti. Textura PNG se decodeaza la 800 x 640 pixeli, citibila. Scor arcoreimg: 100. Numele runtime este acum graf1, in acord cu imaginea reala (numele anterior pisica nu descria sursa). Latimea configurata ramane 0.20 m; imaginea prezentata telefonului trebuie afisata/tiparita cu acea latime, fara decupare.

arcoreimg din ARCore 6.3.5 a compilat cu succes o baza cu toate cele trei surse statice. Scorurile si build-db nu substituie detectia pe telefon si nici rezultatul jobului nativ ScheduleAddImageWithValidationJob pe Android.

## Comutare si operatii asincrone

La intrarea/reactivarea L4 este atribuita explicit biblioteca statica originala. Butonul indica modul static si permite descarcarea imaginii online. In timpul descarcarii, biblioteca statica ramane activa si apasarile suplimentare sunt ignorate. Exista timeout pentru request si asteptarea sesiunii AR.

Se verifica rezultatul HTTP, textura citibila, sesiunea AR, disponibilitatea unei biblioteci mutabile si formatul texturii. Se asteapta jobHandle.IsCompleted, apoi Complete si status Success inainte de activare si mesajul de biblioteca pregatita. Erorile de creare, adaugare si validare sunt raportate cu prefix [Lab4]; butonul permite reincercarea. Nicio eroare de pregatire nu inlocuieste biblioteca statica.

Dupa succes exista o singura biblioteca activa, cea runtime. Butonul permite revenirea la toate cele trei imagini statice. Augmentarile modului anterior sunt ascunse imediat si programate pentru distrugere. Evenimentele pentru GUID-uri dintr-o biblioteca inactiva nu pot recrea continutul vechi.

La iesire se elimina listener-ul, se opreste managerul local, se anuleaza request-ul si coroutinele si se goleste dictionarul. Joburile native nu au API de anulare: daca un job a fost programat, cleanup-ul il completeaza inainte de a elibera resursele, fara a activa biblioteca sau managerul ulterior. Textura descarcata este singura textura distrusa explicit; asset-urile statice/prefab-urile comune nu sunt distruse.

## Verificari si limite

- Compilare cu Roslyn livrat de Unity si referintele/define-urile proiectului din raspunsul de compilare Unity: succes, fara erori C# introduse.
- Verificare structurala a scenei: o Camera, un ARCameraManager, un ARTrackedImageManager, un XR Origin si un AR Session; toate referintele locale fileID se rezolva.
- Verificare textuala a comentariilor originale: pastrate.
- Compilare baza statica prin arcoreimg build-db si evaluarea separata a tuturor celor patru surse: succes.
- Proiect temporar separat, cu aceleasi pachete deja instalate, pentru injectarea evenimentelor de tracking si verificarea augmentarilor. Acesta nu utilizeaza un provider ARCore Android si nu demonstreaza detectia fizica a imaginilor.
- Mediul Unity batch nu a avansat normal cadrele jocului. Distrugerea la final de cadru, detecția efectiva si navigarea completa pe dispozitiv nu sunt declarate validate. Testele de evenimente verifica ascunderea imediata si eliminarea referintelor; codul foloseste Destroy pentru eliberarea la finalul cadrului.

Rezultatul final al testelor de evenimente: ALL TESTS PASSED in Temp/L4Validation/validation-events.log. Au trecut: cele trei imagini statice simultan, 20 actualizari repetate, pozitie/orientare, Limited/None/Tracking cu aceeasi instanta, removed cu ascundere si eliminarea referintei, trei cicluri dezactivare/reactivare cu dezabonare, biblioteca runtime simulata cu un GUID separat, revenire la trei imagini statice, respingerea evenimentelor bibliotecii inactive si anularea request-ului la iesire. Cele trei cicluri sunt teste de lifecycle, nu trei navigari Back pe telefon. Logul proiectului temporar contine si o exceptie UnityEditor.Search.SearchDatabase la initializarea indexului de cautare, independenta de scriptul L4; nu este o eroare de compilare, iar testele au continuat si au trecut.

Artefacte locale: Temp/L4Baseline (copii initiale, PNG descarcat, baza compilata si raspunsul de compilare), Temp/L4Validation (proiectul izolat si logurile testelor). Nu exista scripturi temporare de diagnostic in Assets al proiectului principal.

## Test pe telefon

1. Intra in L4: butonul trebuie sa arate Static: 3 imagini. Prezinta separat fiecare dintre cele trei surse, apoi mai multe simultan. Verifica un singur cub pentru fiecare trackable, centrat pe imagine; misca/roteste imaginile si telefonul.
2. Ascunde/reafiseaza fiecare imagine: cubul trebuie sa dispara la Limited/None si sa reapara la Tracking fara un al doilea cub.
3. Apasa Incarca imaginea online; asteapta mesajul Runtime: graf1 pregatit si logul [Lab4] cu status Success. Prezinta graf1.png la latimea de 20 cm. Cele trei imagini statice nu sunt active in acest mod.
4. Apasa Revino la 3 imagini statice si verifica din nou toate cele trei imagini. Nu este necesara schimbarea manuala a Inspectorului.
5. Back si reintrare de minimum trei ori, inclusiv dupa runtime. La fiecare intrare trebuie sa fie modul static, fara cuburi vechi. Repeta Back imediat dupa pornirea descarcarii si in timpul pregatirii bibliotecii; managerul vechi nu trebuie reactivat.
6. Verifica separat intrarea dupa Augmented Faces si confirma camera din spate si tracking-ul spatial. Nu s-a modificat laboratorul Faces.

Nu s-a executat un build Android sau o sesiune AR pe telefon in aceasta interventie. Validarea nativa a imaginii runtime (statusul jobului pe ARCore) ramane de verificat pe dispozitiv, chiar daca sursa descarcata si calitatea ei au fost verificate aici.




