package com.example.bard.vaffel9;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.support.v7.app.AppCompatActivity;
import android.util.Log;
import android.view.View;
import android.view.animation.AlphaAnimation;
import android.widget.Button;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import java.io.BufferedReader;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.net.ServerSocket;
import java.net.Socket;
public class Vaffel extends AppCompatActivity {
   private String IP = "192.168.1.52";
    private int port = 30000;
    private int portserver = 1754;
    private ProgressBar mProgress, sprogress;
    private int mProgressStatus = 0;
    private Handler mHandler = new Handler();
    private String statusmelding = "Robot er stoppet, trykk start";
    private int stop = 0;
    private  TextView textout;
   private int Progressfinished = 111;
    private  String msg_received;
    private AlphaAnimation buttonClick = new AlphaAnimation(1F, 0.8F);
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_vaffel);
        mProgress = (ProgressBar) findViewById(R.id.progressBar);
        mProgress.setProgress(mProgressStatus);
        sprogress = (ProgressBar) findViewById(R.id.progressBar5);
        sprogress.setVisibility(View.INVISIBLE);
        textout = (TextView) findViewById(R.id.textView);
        textout.setText(statusmelding);
        textout.setTextColor(Color.RED);
        textout.setVisibility(View.INVISIBLE);
        mProgress.setVisibility(View.INVISIBLE);

        new Thread(new Runnable() {
            public void run() {
                while (true) {
                    try {
                        ServerSocket socket = new ServerSocket(portserver);
                        Socket clientSocket = socket.accept();
                        BufferedReader in = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()));
                        while (mProgressStatus < Progressfinished) {
                            msg_received = in.readLine();
                            mProgressStatus = Integer.parseInt(msg_received);
                            mProgress.setProgress(mProgressStatus);
                        }

                    }
                    catch (Exception e)
                    {
                        Log.e("rip", e.getMessage());
                    }
                    mHandler.post(new Runnable() {
                        public void run() {
                            mProgress.setProgress(mProgressStatus);
                        }
                    });
                }
            }
        }).start();
        Button Buttonstart = (Button) findViewById(R.id.buttonStart);
        Buttonstart.setOnClickListener(new View.OnClickListener(){
            public void onClick(View v){
                v.startAnimation(buttonClick);
                sprogress.setVisibility(View.VISIBLE);
                mProgress.setVisibility(View.VISIBLE);
                textout.setVisibility(View.INVISIBLE);
                new Thread(){
                    @Override
                    public void run()
                    {
                        try {
                            Socket socket = new Socket(IP, port);
                            DataOutputStream DOS = new DataOutputStream(socket.getOutputStream());
                            DOS.writeBytes("@start");
                            DOS.flush();
                            DOS.close();
                            socket.close();
                        }
                        catch (IOException e) {
                          

                        }
                    }
                }.start();
            }
        });
        Button Buttonstopp = (Button) findViewById(R.id.buttonStopp);
        Buttonstopp.setOnClickListener(new View.OnClickListener(){
            public void onClick(View v){
                sprogress.setVisibility(View.INVISIBLE);
                mProgress.setVisibility(View.INVISIBLE);
                textout.setVisibility(View.VISIBLE);
                v.startAnimation(buttonClick);
                new Thread(){
                    @Override
                    public void run()
                    {
                        try {
                            Socket socket = new Socket(IP, port);
                            DataOutputStream DOS = new DataOutputStream(socket.getOutputStream());
                            DOS.writeBytes("@stopp");
                            DOS.flush();
                            DOS.close();
                            socket.close();
                        }
                        catch (IOException e) {


                        }
                    }
                }.start();
            }
        });
    }
}
