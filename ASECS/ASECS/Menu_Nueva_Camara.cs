﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ozeki.Camera;
using Ozeki.Media;
using System.Threading;
using System.Net;
using odm.core;
using onvif.services;
using Microsoft;
using onvif.utils;
using utils;
using System.Globalization;
using System.IO;

namespace ASECS
{
    public partial class Menu_Nueva_Camara : Form
    {
        //Objetos Programa

        Menu_Nueva_Camara_Aspectos Aspectos;
        Variables_Menu_Nueva_Camara Variables_Globales;
        Menu_Nueva_Camara_Metodos Metodos;
        AxoPlayerLib.AxoPlayer Nuevo_Reproductor_Camara;
        Vlc.DotNet.Forms.VlcControl Nuevo_Grabador_VLC;
        Menu_Principal formulario_Menu_Principal;
        Camara Nueva_Camara;

        //Hilos Programa
        Thread Buscar_Camaras_Hilo;
        Thread Obtener_Ursl_Hilo;

        public Menu_Nueva_Camara(Menu_Principal formulario_Menu_Principal)
        {
            this.formulario_Menu_Principal = formulario_Menu_Principal;
            InitializeComponent();
        }

        private void Menu_Nueva_Camara_Load(object sender, System.EventArgs e)
        {
            Inicializar_Objetos();
            Aspectos.Acomodar_Elementos();
            Texto_Alias.MaxLength = 30;
        }

        public void Inicializar_Objetos()
        {
            Aspectos = new Menu_Nueva_Camara_Aspectos(this);
            Variables_Globales = new Variables_Menu_Nueva_Camara();
            Metodos = new Menu_Nueva_Camara_Metodos(this);

        }

        public struct ListItem
        {
            private readonly string _name;
            internal readonly string[] Value;

            public ListItem(string name, string[] value)
            {
                _name = name;
                Value = value;
            }

            public override string ToString()
            {
                return _name;
            }
        }

        void Obtener_IP_Camaras()
        {
            //Semaforo para obtener ips de camaras
            //Variables_Globales.Semaforo_Control_Uris.WaitOne();

            IPCameraFactory.DeviceDiscovered += Camaras_IP_Descubiertas;
            IPCameraFactory.DiscoverDevices();

            //Semaforo liberar control de uris
            //Variables_Globales.Semaforo_Control_Uris.Release();
        }

        private void Camaras_IP_Descubiertas(object sender, DiscoveryEventArgs e)
        {
            if (this.Lista_Camaras_Disponibles.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)delegate()
                {
                    Lista_Camaras_Disponibles.Items.Add(e.Device.Uri);
                    Variables_Globales.Urls.Add(e.Device.Uri);
                });
            }
            else
            {
                Lista_Camaras_Disponibles.Items.Add(e.Device.Uri);
                Variables_Globales.Urls.Add(e.Device.Uri);
            }

        }

        private void Boton_Actualizar_Camaras_Click_1(object sender, EventArgs e)
        {
            Variables_Globales.Urls_Onvif = new List<object>();
            Variables_Globales.Urls = new List<Uri>();
            Variables_Globales.ONVIFDevices = new List<DeviceDescriptionHolder>();
            Lista_Camaras_Disponibles.Items.Clear();
            Lista_Url_Camara_Seleccionada.Items.Clear();
            Metodos.Limpiar_Campos_Actualizar();
            IPCameraFactory.DeviceDiscovered -= Camaras_IP_Descubiertas;

            //Obtiene la lista de camaras ip para seleccionar
            Buscar_Camaras_Hilo = new Thread(() => Obtener_IP_Camaras());
            Buscar_Camaras_Hilo.Start();

            Boton_Seleccionar_Camara.Enabled = true;
        }

        private void Asignar_Parametros_Variables_Globales()
        {
            Variables_Globales.Usuario = Texto_Usuario.Text;
            Variables_Globales.Contraseña = Texto_Contraseña.Text;
            Variables_Globales.Puerto_CGI = Texto_Puerto_CGI.Text;
            Variables_Globales.Direccion_IP = Texto_Direccion_IP.Text;
            Variables_Globales.Url_RSTP = Convert.ToString(Variables_Globales.Urls_Onvif[Lista_Url_Camara_Seleccionada.SelectedIndex]);
            Variables_Globales.Puerto_RSTP = Texto_Puerto_RTSP.Text;
        }

        public void Asignar_Parametros_Objeto_Nueva_Camara(int id_Camara)
        {
            Nueva_Camara = new Camara();

            Nueva_Camara.Camara_ID = id_Camara;
            Nueva_Camara.Alias = Texto_Alias.Text;
            Nueva_Camara.Usuario = Texto_Usuario.Text;
            Nueva_Camara.Contraseña = Texto_Contraseña.Text;
            Nueva_Camara.Direccion_IP = Texto_Direccion_IP.Text;
            Nueva_Camara.Url_RSTP = Variables_Globales.Url_RSTP;
            Nueva_Camara.Puerto_CGI = Variables_Globales.Puerto_CGI;
            Nueva_Camara.Puerto_RSTP = Variables_Globales.Puerto_RSTP;
        }

        private void Boton_Agregar_Camara_Click(object sender, System.EventArgs e)
        {
            bool resultado_busqueda;

            try
            {
                string text = Lista_Url_Camara_Seleccionada.GetItemText(Lista_Url_Camara_Seleccionada.SelectedItem);
                int id_Usuario, id_Camara;
                if (text != "")
                {
                    if (Texto_Alias.Text != "")
                    {
                        resultado_busqueda = formulario_Menu_Principal.Buscar_Camaras_Alias_Nodos_Existentes(Texto_Alias.Text);

                        if (resultado_busqueda == false)
                        {

                            int respuesta = Metodos.Verificar_Puerto_CGI();

                            if (respuesta == 1 || respuesta == 3)
                            {
                                try
                                {
                                    Asignar_Parametros_Variables_Globales();

                                    //agrega el reproductor al menu principal
                                    formulario_Menu_Principal.Menu_Lista_Camaras.Controls.Add(Nuevo_Reproductor_Camara = new AxoPlayerLib.AxoPlayer());

                                    Nuevo_Grabador_VLC = new Vlc.DotNet.Forms.VlcControl();
                                    Nuevo_Grabador_VLC.VlcLibDirectory = new DirectoryInfo(formulario_Menu_Principal.Sesion_Usuario.Directorio_VLC);

                                    //Nuevo_Grabador_VLC.VlcLibDirectory = new DirectoryInfo(@"C:\\Program Files (x86)\\VideoLAN\\VLC");
                                    formulario_Menu_Principal.Menu_Lista_VLC.Controls.Add(Nuevo_Grabador_VLC);

                                    Nuevo_Grabador_VLC.Width = 50;
                                    Nuevo_Grabador_VLC.Height = 50;

                                    //Propiedades del reproductor
                                    Nuevo_Reproductor_Camara.Width = 400;
                                    Nuevo_Reproductor_Camara.Height = 300;

                                    //Inicia el streaming con los datos obtenidos si el streaming esta activo
                                    if (formulario_Menu_Principal.Variables_Globales.Streaming_Activo == true)
                                    {
                                        Nuevo_Reproductor_Camara.PlayVideo(Variables_Globales.Usuario, Variables_Globales.Contraseña, Variables_Globales.Direccion_IP, Convert.ToInt32(Variables_Globales.Puerto_CGI), 0, 0);
                                    }
                                    else
                                        if (formulario_Menu_Principal.Menu_Lista_Camaras.Controls.Count == 1)
                                        {
                                            Nuevo_Reproductor_Camara.PlayVideo(Variables_Globales.Usuario, Variables_Globales.Contraseña, Variables_Globales.Direccion_IP, Convert.ToInt32(Variables_Globales.Puerto_CGI), 0, 0);
                                            formulario_Menu_Principal.Variables_Globales.Streaming_Activo = true;
                                        }

                                    formulario_Menu_Principal.Metodos.Insertar_Camara_BD(this);
                                    id_Usuario = formulario_Menu_Principal.Metodos.Obtener_ID_Usuario_BD(this);
                                    id_Camara = formulario_Menu_Principal.Metodos.Obtener_Ultimo_ID_Camara_BD();

                                    //Asignamos los parametros del objeto
                                    Asignar_Parametros_Objeto_Nueva_Camara(id_Camara);

                                    //Crea el nodo de referencia para mostrar las camaras al usuario
                                    formulario_Menu_Principal.Crear_Nodos_Camaras(Nueva_Camara);

                                    formulario_Menu_Principal.Metodos.Insertar_Tabla_Usuario_Camara_BD(id_Camara, id_Usuario);

                                    this.Close();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);

                                    MessageBox.Show("Hubo un error inesperado",
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error,
                                    MessageBoxDefaultButton.Button1);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Porfavor introduce el puerto CGI",
                                "Aviso",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Exclamation,
                                MessageBoxDefaultButton.Button1);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: El Nombre ya esta registrado",
                            "Aviso",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation,
                            MessageBoxDefaultButton.Button1);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Introduce un Nombre para la camara",
                        "Aviso",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1);
                    }
                }
                else
                {
                    MessageBox.Show("Selecciona una camara",
                    "Aviso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);

                MessageBox.Show("Hubo un error inesperado",
                "Aviso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation,
                MessageBoxDefaultButton.Button1);
            }
        }

        private void Boton_Seleccionar_Camara_Click(object sender, EventArgs e)
        {
            bool respuesta;

            Lista_Url_Camara_Seleccionada.Items.Clear();
            respuesta = Metodos.Verificar_Campos_Boton_Seleccionar();

            if (!respuesta)
            {
                MessageBox.Show("Porfavor completa todos los campos.",
                "Aviso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation,
                MessageBoxDefaultButton.Button1);
            }
            else
            {
                //Obtiene las urls del onvif para mostrar el streaming
                Obtener_Ursl_Hilo = new Thread(() => Metodos.Obtener_Listas_Url(ref Variables_Globales));
                Obtener_Ursl_Hilo.Start();
                Obtener_Ursl_Hilo.Join();

                if (Lista_Camaras_Disponibles.SelectedIndex != -1)
                {
                    Metodos.Obtener_Onvif_Url(ref Variables_Globales);
                }
                else
                {
                    MessageBox.Show("Porfavor Selecciona Una direccion IP.",
                    "Aviso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                }
            }
          
        }

        private void Checkeo_Cambio_Puerto_CGI_CheckedChanged(object sender, EventArgs e)
        {
            if(Checkeo_Cambio_Puerto_CGI.Checked == true)
            {
                Texto_Puerto_CGI.ReadOnly = false;
            }
            else
            {
                Texto_Puerto_CGI.Text = "81";
                Texto_Puerto_CGI.ReadOnly = true;
            }
        }

        private void Texto_Puerto_CGI_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void Texto_Alias_TextChanged(object sender, EventArgs e)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(Texto_Alias.Text,@"^[a-zA-Z0-9\s\báéíóúÁÉÍÓÚñÑ]*$"))
            {
                MessageBox.Show("No se admiten estos simbolos.",
                "Aviso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation,
                MessageBoxDefaultButton.Button1);
                Texto_Alias.Text = "";

            }
        }

    }
}
