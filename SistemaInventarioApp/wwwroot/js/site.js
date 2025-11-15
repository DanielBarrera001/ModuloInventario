// Espera a que el documento esté completamente cargado
$(document).ready(function () {

    // Altura de scroll en píxeles para activar el cambio (por ejemplo, justo debajo de la imagen)
    // Usamos la altura del banner + un pequeño margen
    const SCROLL_TRIGGER = $('#hero-banner').height() || 350;

    const $body = $('body');

    // Escucha el evento de scroll en la ventana
    $(window).on('scroll', function () {
        // Obtiene la posición actual del scroll vertical
        let scrollPosition = $(window).scrollTop();

        // Comprueba si el scroll ha superado el punto de activación
        if (scrollPosition > SCROLL_TRIGGER) {
            // Si superó, agrega la clase 'scrolled' al body
            if (!$body.hasClass('scrolled')) {
                $body.addClass('scrolled');
            }
        } else {
            // Si no superó, quita la clase
            if ($body.hasClass('scrolled')) {
                $body.removeClass('scrolled');
            }
        }
    });

    // Disparar el scroll una vez al cargar para verificar si la página ya cargó scrolleada
    $(window).trigger('scroll');
});