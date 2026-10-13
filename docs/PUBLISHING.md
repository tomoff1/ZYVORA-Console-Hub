# Publication gratuite

1. Créer un dépôt GitHub public sous une identité de projet, par exemple une organisation ZYVORA si disponible. Ne jamais saisir une identité personnelle dans les métadonnées du produit.
2. Envoyer les sources ; exclure .local, .build, bin, obj et dist. Aucun contenu de console ni identifiant ne doit être publié.
3. Dans Settings > Pages, sélectionner GitHub Actions. Le workflow pages.yml prépare config.json avec le dépôt courant et publie le dossier site. Les téléchargements n'apparaissent que lorsqu'une vraie release publique existe.
4. Créer le tag v0.1.0. Le workflow release.yml compile, teste, vérifie l'installateur et crée une release avec installateur, portable et SHA256SUMS.txt.
5. Vérifier manuellement installation et désinstallation sur une machine Windows propre, puis documenter les résultats avant une annonce publique.

Les liens du centre de support ciblent les Issues du dépôt une fois celui-ci configuré. Aucun faux lien de téléchargement ou de support n'est utilisé pendant la préparation.

La mise à jour automatique de l'application n'est pas active. Il faut définir le dépôt officiel, un format de manifeste et une stratégie de signature/authentification des versions avant d'activer un exécuteur de mises à jour. GitHub Pages et Releases ne nécessitent aucun VPS.
