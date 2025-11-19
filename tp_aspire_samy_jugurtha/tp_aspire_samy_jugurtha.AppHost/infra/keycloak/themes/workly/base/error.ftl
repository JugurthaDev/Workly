<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="utf-8" />
  <title>Erreur - Workly</title>
  <link rel="stylesheet" href="${url.resourcesPath}/css/workly.css?v=1" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
</head>
<body>
<div id="kc-container-wrapper" class="workly-login-wrapper">
  <header id="kc-header">
      <div id="kc-header-wrapper">Workly</div>
  </header>
  <main id="kc-content" class="workly-card">
    <h1>Une erreur est survenue</h1>
    <p>Requête invalide.</p>
    <#if message??>
      <div class="alert error">${message.summary?no_esc}</div>
    </#if>
    <div class="actions">
      <a class="btn" href="/">Retour</a>
    </div>
  </main>
  <footer class="workly-footer">
      <small>&copy; 2025 Workly</small>
  </footer>
</div>
</body>
</html>
