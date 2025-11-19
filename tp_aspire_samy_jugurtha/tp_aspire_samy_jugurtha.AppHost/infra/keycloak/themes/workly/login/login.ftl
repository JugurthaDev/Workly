<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="utf-8" />
    <title>Connexion - Workly</title>
    <link rel="stylesheet" href="css/workly.css" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
</head>
<body>
<div id="kc-container-wrapper" class="workly-login-wrapper">
    <header id="kc-header">
        <div id="kc-header-wrapper">Workly</div>
    </header>
    <main id="kc-content" class="workly-card">
        <#-- Message global (erreur, info) -->
        <#if message??>
            <div class="alert ${message.type!'info'}">${message.summary?no_esc}</div>
        </#if>
        <form id="kc-form-login" action="${url.loginAction}" method="post" novalidate>
            <input type="hidden" name="credentialId" />
            <div class="form-group">
                <label for="username">Email</label>
                <input tabindex="1" id="username" name="username" type="text" autofocus autocomplete="username" placeholder="email@exemple.fr" />
                <#if messagesPerField.exists('username')>
                  <span class="field-error">${messagesPerField.get('username')?no_esc}</span>
                </#if>
            </div>
            <div class="form-group">
                <label for="password">Mot de passe</label>
                <input tabindex="2" id="password" name="password" type="password" autocomplete="current-password" />
                <#if messagesPerField.exists('password')>
                  <span class="field-error">${messagesPerField.get('password')?no_esc}</span>
                </#if>
            </div>
            <#if realm.rememberMe?? && realm.rememberMe>
              <div class="form-group small">
                <label><input type="checkbox" name="rememberMe" <#if login.rememberMe?? && login.rememberMe>checked</#if> /> Se souvenir de moi</label>
              </div>
            </#if>
            <div class="actions">
                <button tabindex="3" id="kc-login" type="submit">Se connecter</button>
            </div>
        </form>
        <div class="secondary-links">
            <#if realm.resetPasswordAllowed>
              <a href="${url.loginResetCredentials}" class="link">Mot de passe oublié ?</a>
            </#if>
            <#if realm.registrationAllowed>
              <a href="${url.registrationUrl}" class="link accent">Créer un compte</a>
            </#if>
        </div>
    </main>
    <footer class="workly-footer">
        <small>&copy; 2025 Workly</small>
    </footer>
</div>
</body>
</html>
