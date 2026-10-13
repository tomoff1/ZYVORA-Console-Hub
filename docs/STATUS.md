# ZYVORA 0.1.0 — rapport de validation

Les fonctions ne sont pas toutes validées sur matériel. Voir VALIDATION.md pour les résultats exacts.

| Fonction | Implémentation | Validation matérielle |
|---|---|---|
| Client FTP : liste, upload, download, mkdir, rename, delete | Implémentée, tests protocolaires locaux | Lecture seule à vérifier ; aucune mutation automatique |
| Édition texte | Download / édition / upload avec confirmation | À vérifier manuellement |
| Découverte IPv4 | Sous-réseaux privés /24 ou plus petits, ports 1337/2121 ; identification bannière | À vérifier ; IP manuelle possible |
| Loader TCP ELF/BIN | Envoi local avec progression et annulation | Exécution non certifiée |
| Payload Manager HTTP | Version et upload via API v0.5.2 | Version accessible ; upload non exécuté automatiquement |
| Catalogue | Une entrée d'origine, licence et hash publiés | Compatibilité rapportée par source uniquement |
| Bibliothèque PKG/homebrew | Index local, hash et transfert FTP | Installation PKG non implémentée |
| Copies FTP | Récursion et manifeste SHA-256 ; vérification | Restauration et déchiffrement non implémentés |
| Diagnostic etaHEN | Règles transparentes sur texte importé | Conseils, pas identification certaine de la cause |
| Thèmes et animation | Deux fonds, animation de connexion | Réglages de thème non persistés |
| Application PS5 | Étude SDK et source compagnon ELF | Pas de PKG PS5, pas de validation console |
| Cheats/mods, jaquettes et mises à jour de jeux | Non implémentés | Non disponibles |
| Extensions et outils de debug PS5 | Non implémentés | Non disponibles |
| Mise à jour automatique ZYVORA | Non implémentée | Non disponible |
| Site GitHub Pages / Releases | Sources et workflow de publication | Nécessite un dépôt GitHub et sa configuration |

Aucun faux statut « installé », « compatible vérifié » ou « exécution réussie » n'est affiché. Le firmware est saisi, jamais déduit d'un port réseau ouvert. Aucun jeu commercial, clé, exploit ou binaire tiers n'est livré.
