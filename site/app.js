'use strict';
(async()=>{
  const status=document.getElementById('release-status');
  try{
    const config=await fetch('config.json').then(r=>{if(!r.ok)throw Error('Configuration absente');return r.json();});
    if(!config.repository)return;
    if(!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(config.repository))throw Error('Dépôt invalide');
    const base='https://github.com/'+config.repository;
    for(const [id,path] of [['all-releases','/releases'],['issue-link','/issues/new']]){const a=document.getElementById(id);a.href=base+path;a.hidden=false;}
    const response=await fetch('https://api.github.com/repos/'+config.repository+'/releases?per_page=10');
    if(!response.ok){status.textContent='Aucune version publique disponible. Consultez les versions du dépôt.';return;}
    const releases=await response.json();const release=Array.isArray(releases)?releases.find(r=>!r.draft):null;
    if(!release){status.textContent='Aucune version publique disponible pour le moment.';return;}
    status.textContent=(release.prerelease?'Préversion publiée : ':'Version publiée : ')+release.tag_name;
    for(const asset of release.assets){if(!/^(ZYVORA-.*\.(exe|zip)|SHA256SUMS\.txt)$/.test(asset.name))continue;const u=new URL(asset.browser_download_url);if(u.origin!=='https://github.com'||!u.pathname.startsWith('/'+config.repository+'/releases/download/'))continue;const a=document.createElement('a');a.className='button primary';a.href=u.href;a.textContent=asset.name+' ↓';document.getElementById('downloads').append(a);}
  }catch{status.textContent='Informations de version indisponibles. Réessayez plus tard.';}
})();
