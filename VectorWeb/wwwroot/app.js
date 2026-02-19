window.scrollSugerenciaVisible = (contenedor, indice) => {
    if (!contenedor || indice < 0) {
        return;
    }

    const item = contenedor.querySelector(`[data-indice-sugerencia="${indice}"]`);
    if (!item) {
        return;
    }

    const topItem = item.offsetTop;
    const bottomItem = topItem + item.offsetHeight;
    const topVisible = contenedor.scrollTop;
    const bottomVisible = topVisible + contenedor.clientHeight;

    if (topItem < topVisible) {
        contenedor.scrollTop = topItem;
        return;
    }

    if (bottomItem > bottomVisible) {
        contenedor.scrollTop = bottomItem - contenedor.clientHeight;
    }
};

window.registrarOcultarSugerenciasOficina = (contenedor, dotNetHelper) => {
    if (!contenedor || !dotNetHelper) {
        return;
    }

    const manejarClickDocumento = (evento) => {
        if (!contenedor.contains(evento.target)) {
            dotNetHelper.invokeMethodAsync('OcultarSugerenciasOficina');
        }
    };

    document.addEventListener('click', manejarClickDocumento, true);
    contenedor.__ocultarSugerenciasOficinaHandler = manejarClickDocumento;
};

window.desregistrarOcultarSugerenciasOficina = (contenedor) => {
    const handler = contenedor?.__ocultarSugerenciasOficinaHandler;
    if (!handler) {
        return;
    }

    document.removeEventListener('click', handler, true);
    delete contenedor.__ocultarSugerenciasOficinaHandler;
};

window.hacerScrollHaciaElemento = (elementoId) => {
    const elemento = document.getElementById(elementoId);
    if (elemento) {
        elemento.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};

(() => {
    const SELECTOR_MENSAJES = '.alert[role="alert"], .validation-message';
    let ultimoScrollAt = 0;

    const estaVisible = (elemento) => {
        const rect = elemento.getBoundingClientRect();
        return rect.top >= 0 && rect.bottom <= window.innerHeight;
    };

    const hacerScrollAlMensaje = (elemento) => {
        if (!elemento || !(elemento instanceof HTMLElement)) {
            return;
        }

        const ahora = Date.now();
        if (ahora - ultimoScrollAt < 250) {
            return;
        }

        if (estaVisible(elemento)) {
            return;
        }

        ultimoScrollAt = ahora;
        elemento.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    const procesarMensajes = (nodo) => {
        if (!(nodo instanceof HTMLElement)) {
            return;
        }

        if (nodo.matches(SELECTOR_MENSAJES)) {
            hacerScrollAlMensaje(nodo);
            return;
        }

        const primerMensaje = nodo.querySelector(SELECTOR_MENSAJES);
        if (primerMensaje) {
            hacerScrollAlMensaje(primerMensaje);
        }
    };

    const observer = new MutationObserver((mutaciones) => {
        for (const mutacion of mutaciones) {
            for (const nodo of mutacion.addedNodes) {
                procesarMensajes(nodo);
            }

            if (mutacion.type === 'characterData' && mutacion.target?.parentElement) {
                procesarMensajes(mutacion.target.parentElement);
            }
        }
    });

    const iniciarObservador = () => {
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true,
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciarObservador, { once: true });
    } else {
        iniciarObservador();
    }
})();
