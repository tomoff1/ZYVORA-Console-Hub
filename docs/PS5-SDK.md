# Compagnon PS5 : décision technique

Sources consultées le 13 octobre 2026 :
- https://github.com/ps5-payload-dev/sdk (SDK public, ELF, environnement POSIX, LLVM 18, GPLv3+ sauf fichiers BSD)
- https://github.com/PS5Dev/PS5SDK (payloads et homebrew simple)
- https://github.com/etaHEN/etaHEN (FTP, loader, API et plugins)
- https://github.com/GronedWaffel/etahen-11.00-13.60 (port communautaire 13.60)

Un ELF compagnon est réalisable avec le SDK public. Il est distinct d'une application graphique installable au menu de la console. La création d'un PKG installable et son lancement sur 13.60 ne sont pas validés ici. Aucun SDK propriétaire ni clé Sony n'est utilisé.

Le source ps5/main.c fournit un petit compagnon qui affiche une notification et écrit un rapport propre à ZYVORA dans /data/zyvora. Aucun patch noyau, exploit ou modification des autres logiciels n'est inclus. Ce prototype ne remplace pas un explorateur PS5 et n'est pas annoncé comme application installable.

Compiler avec un SDK public installé :
```sh
export PS5_PAYLOAD_SDK=/path/to/ps5-payload-sdk
make -C ps5
```
Le résultat doit être testé explicitement avec un loader compatible, puis vérifié sur console. Ne pas charger automatiquement le compagnon. Le code compagnon est sous GPL-3.0-or-later, selon sa déclaration SPDX ; les autres sources ZYVORA sont sous MIT.

Tentative locale : SDK v0.44 téléchargé et vérifié contre le digest de la release GitHub (`4d06eab777bb135705b5a6816688c9132efb246093fe606ac488ab6cf65ca5b0`). Compilation non réalisée : LLVM 18 absent et Ubuntu WSL renvoie des erreurs I/O / système de fichiers en lecture seule ; le disque système a 0 octet libre. Aucune réparation WSL ni purge de fichiers de l'utilisateur n'a été effectuée. Aucun ELF ou PKG PS5 n'est livré comme opérationnel.

## Validation distante et matérielle

La compilation a ensuite réussi sur GitHub Actions Ubuntu 24.04 avec LLVM 18 et le SDK public v0.44 vérifié. [Exécution CI](https://github.com/tomoff1/ZYVORA-Console-Hub/actions/runs/38433203244). L'artefact ZIP a été téléchargé après vérification de son digest GitHub. Le fichier obtenu fait 110 824 octets et son en-tête ELF64 x86-64 est correct.

Un test de démarrage sur la PS5 déclarée 13.60 a réussi : envoi au loader TCP, rapport produit dans le dossier propre à ZYVORA, puis lecture FTP et vérification des champs product/version. Le dossier était absent avant le test et ses fichiers de test ont été supprimés après lecture. Cela valide uniquement ce petit programme et ce chemin d'exécution ; aucune application graphique ou installation PKG n'a été testée.

Sources correspondantes : ce dépôt pour main.c/Makefile, et [SDK v0.44](https://github.com/ps5-payload-dev/sdk/tree/v0.44) pour les composants SDK. Licence GPL-3.0-or-later, texte complet dans ps5/COPYING. Le prototype reste expérimental.
