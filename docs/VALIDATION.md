# Validation ZYVORA 0.1.0

Date : 13 octobre 2026. Windows x64, SDK .NET 8.0.425. Aucun chemin personnel, adresse console ou journal brut n'est publié dans ce rapport.

## Tests réalisés

- Compilation Desktop WPF Release réussie.
- Publication autonome x64 réussie via MSBuild ; aucun .NET séparé nécessaire pour les binaires autonomes.
- 19 contrôles locaux réussis : réseau privé, injection FTP, traversée de chemins, MLSD et LIST avec espaces, mode EPSV/MLSD et repli PASV/LIST, transferts binaires identiques, rename/delete, sauvegardes et détection de corruption, payload TCP exact, annulation, garde du catalogue et règle de diagnostic.
- Console PS5 déclarée 13.60 : bannière FTP réelle sur 1337, authentification, liste /data, version Payload Manager HTTP 0.5.2 sur 8084.
- Console réelle : création d'un dossier de validation isolé, upload puis download d'un petit fichier texte, SHA-256 identique, suppression du fichier et dossier de test. Aucun payload exécuté.
- Catalogue : téléchargement depuis GitHub et SHA-256 identique à celui publié dans la source. Fichier de test supprimé, jamais exécuté ni ajouté aux distributions.
- Installateur : manifeste de chaque fichier vérifié, archive extraite avec succès dans un répertoire de test.
- Interface : démarrage du binaire autonome et rendu WPF PNG réel, inspecté visuellement. Capture hors connexion destinée au site.
- JavaScript du site : contrôle syntaxique Node.

## Limitations des essais

- Installation dans le menu Démarrer, inscription dans les applications Windows et cycle complet de désinstallation pas validés sur une machine propre. Le disque C de cet environnement est plein. L'installateur implémente ces actions, mais seul son contenu et son extraction ont été testés.
- Le runtime single-file utilise un dossier temporaire d'extraction. Les tests du binaire ont utilisé DOTNET_BUNDLE_EXTRACT_BASE_DIR dans le projet sur D pour éviter le disque C plein.
- UI : contrôles visuels et démarrage, pas de test manuel exhaustif de chaque dialogue.
- Port loader 9021 ouvert ; envoi TCP testé sur fixture uniquement. Pas de validation d'exécution console ni d'upload Payload Manager sur matériel.
- Découverte locale : algorithme implémenté, pas certifié sur plusieurs topologies ; adresse manuelle utilisée pour le test console.
- FTP en clair, noms UTF-8 et listes Unix/MLSD ; listes DOS et liens symboliques non pris en charge. Aucun hash distant automatique : la validation matérielle compare le fichier original avec sa copie téléchargée.
- Sauvegardes : copies de fichiers accessibles, pas de restauration ni déchiffrement de sauvegardes de jeux.
- Site non publié et non testé dans un navigateur : l'outil navigateur n'a pas pu démarrer faute d'espace système. Aucun lien de téléchargement public inventé.
- WSL en erreur I/O / lecture seule, LLVM requis absent : compagnon PS5 fourni en source uniquement, aucun PKG natif.
- Mises à jour automatiques, cheats/mods, plugins, jaquettes et installation PKG ne sont pas implémentés.

Une première tentative de téléchargement dans le TEMP système a échoué faute d'espace. Le contrôle automatique a refusé une nouvelle tentative avant vérification de la taille et du disque. Après mesure (payload 36 746 616 octets, D disposant de plus de 300 Go libres), un test limité sur D a réussi. Le client contrôle maintenant espace disponible et taille maximale de 512 Mio pour le catalogue.
