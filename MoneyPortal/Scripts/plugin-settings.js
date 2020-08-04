﻿//BLOCK CONTROL BUTTONS
$('.btn-block-minimize').on("click", function (e) {
    $(e.currentTarget).toggleClass(['fa-chevron-up', 'fa-chevron-down'])
    $(e.currentTarget).closest('.block').children('.collapsable').slideToggle()
});
$('.btn-block-close').on("click", function (e) {
    $(e.currentTarget).closest('.block').parent().hide()
});

//TOASTR ALERTS SETUP
toastr.options = {
    "closeButton": true,
    "debug": false,
    "newestOnTop": true,
    "progressBar": true,
    "positionClass": "toast-top-right",
    "preventDuplicates": false,
    "onclick": null,
    "showDuration": "300",
    "hideDuration": "1000",
    "timeOut": "8000",
    "extendedTimeOut": "2000",
    "showEasing": "swing",
    "hideEasing": "linear",
    "showMethod": "fadeIn",
    "hideMethod": "fadeOut"
}

//CURRENCY FORMATTER
var formatter = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
});

//AJAX PARTIAL TEST
//< button type = "button" class="btn btn-primary" id = "freshTest" > Refresh Side Menu</button > *@
//$('#freshTest').on("click", () => {
//    $('#SideMenu').load('@Url.Action("SideMenuRefresh", "Dashboard")');
//})