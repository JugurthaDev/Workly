<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="utf-8" />
  <title>Inscription - Workly</title>
  <link rel="stylesheet" href="css/workly.css" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
</head>
<body>
<div id="kc-container-wrapper" class="workly-login-wrapper">
  <header id="kc-header">
    <div id="kc-header-wrapper">Créer votre compte Workly</div>
  </header>
  <main id="kc-content" class="workly-card">
    <#if message??>
      <div class="alert ${message.type!'info'}">${message.summary?no_esc}</div>
    </#if>
    <form id="kc-register-form" action="${url.registrationAction}" method="post" novalidate>
      <div class="form-group">
        <label for="firstName">Prénom</label>
        <input id="firstName" name="firstName" type="text" autofocus />
        <#if messagesPerField.exists('firstName')>
          <span class="field-error">${messagesPerField.get('firstName')?no_esc}</span>
        </#if>
      </div>
      <div class="form-group">
        <label for="lastName">Nom</label>
        <input id="lastName" name="lastName" type="text" />
        <#if messagesPerField.exists('lastName')>
          <span class="field-error">${messagesPerField.get('lastName')?no_esc}</span>
        </#if>
      </div>
      <div class="form-group">
        <label for="email">Email</label>
        <input id="email" name="email" type="email" autocomplete="email" />
        <#if messagesPerField.exists('email')>
          <span class="field-error">${messagesPerField.get('email')?no_esc}</span>
        </#if>
      </div>
      <div class="form-group">
        <label for="username">Nom d'utilisateur</label>
        <input id="username" name="username" type="text" autocomplete="username" />
        <#if messagesPerField.exists('username')>
          <span class="field-error">${messagesPerField.get('username')?no_esc}</span>
        </#if>
      </div>
      <div class="form-group">
        <label for="password">Mot de passe</label>
        <input id="password" name="password" type="password" autocomplete="new-password" />
        <#if messagesPerField.exists('password')>
          <span class="field-error">${messagesPerField.get('password')?no_esc}</span>
        </#if>
      </div>
      <div class="form-group">
        <label for="password-confirm">Confirmer le mot de passe</label>
        <input id="password-confirm" name="password-confirm" type="password" autocomplete="new-password" />
        <#if messagesPerField.exists('password-confirm')>
          <span class="field-error">${messagesPerField.get('password-confirm')?no_esc}</span>
        </#if>
      </div>
      <div class="actions">
        <button type="submit">Créer le compte</button>
      </div>
    </form>
    <div class="secondary-links">
      <a href="${url.loginUrl}" class="link">Déjà un compte ? Se connecter</a>
    </div>
  </main>
  <footer class="workly-footer">
    <small>&copy; 2025 Workly</small>
  </footer>
</div>
</body>
</html>
