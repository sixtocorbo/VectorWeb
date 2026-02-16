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

window.scrollToAnchorCentered = (anchorId) => {
    if (!anchorId) {
        return;
    }

    const section = document.getElementById(anchorId);
    if (!section) {
        return;
    }

    section.scrollIntoView({
        behavior: 'smooth',
        block: 'center'
    });

    if (window.history?.replaceState) {
        window.history.replaceState(null, '', `#${anchorId}`);
    }
};
